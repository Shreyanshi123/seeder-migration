using LiteX.Storage.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using Quartz;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Elastic;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;
using SearchLego.DataSeerer.Integration;
using SearchLego.DataSeerer.Integration.RssFeed;
using SearchLego.DataSeerer.Integration.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SearchLego.DataSeeder.Connector;

namespace SearchLego.DataSeeder.Schedular
{
    [DisallowConcurrentExecution]
    public class CognitiveDataIngestionJob : IJob
    {
        private readonly ILogger<dynamic> _logger;
        private readonly IConfiguration _config;
        private readonly ApplicationConfig appConfig;
        private readonly ILiteXStorageProviderFactory _liteXStorageProviderFactory;
        private readonly IUtilityFunctions _iUtilityFunctions;
        private readonly static Object lockJobTracking = new Object();
        private IRssFeed rssFeed;
        private IFileIngestion fileIngestion;
        private readonly ITenantService _iTenantService;

        public CognitiveDataIngestionJob(ILogger<dynamic> logger,
            IConfiguration config,
            ILiteXStorageProviderFactory liteXStorageProviderFactory,
            IUtilityFunctions utilityFunctions, ITenantService iTenantService)
        {
            _logger = logger;
            _config = config;
            _logger.LogInformation($"Cognitive Data Ingestion constructor called.");
            appConfig = _config.GetSection("jobs").Get<IList<ApplicationConfig>>().FirstOrDefault();
            _liteXStorageProviderFactory = liteXStorageProviderFactory;
            _iUtilityFunctions = utilityFunctions;
            _logger.LogInformation($"ElasticSearch and SQL Server connections are established.");
            _iTenantService = iTenantService;
        }

        public Task Execute(IJobExecutionContext context)
        {
            var jobId = context.JobDetail.Description;
            try
            {
                IMongoConfigFactory iCognitiveConfiguration = new MongoClientSetting().GetDBObject(_logger, appConfig, MongoStaticName.CognitiveConfiguration);
                IMongoConfigFactory featureConfigurationFactory = new MongoClientSetting().GetDBObject(_logger, appConfig, MongoStaticName.FeatureConfiguration);
                IMongoConfigFactory iClientConfigFactory = new MongoClientSetting().GetDBObject(_logger, appConfig, MongoStaticName.Configuration);
                IMongoConfigFactory jobClientFeatureMappingFactory = new MongoClientSetting().GetDBObject(_logger, appConfig, MongoStaticName.JobClientFeatureMapping);
                IMongoConfigFactory JobTrackingFactory = new MongoClientSetting().GetDBObject(_logger, appConfig, MongoStaticName.JobTracking);
                IMongoConfigFactory jobHistoryFactory = new MongoClientSetting().GetDBObject(_logger, appConfig, MongoStaticName.JobHistory);
                IMongoConfigFactory nerEntityRulerFactory = new MongoClientSetting().GetDBObject(_logger, appConfig, MongoStaticName.NEREntityRuler);
                IElasticConnector iElasticConnector = new ElasticConnector(_logger, appConfig);
                iElasticConnector.ElasticConnect();
                IElasticIndexBuilder iElasticIndexBuilder = new ElasticIndexBuilder(iElasticConnector);
                ICognitiveDataElasticService iCognitiveDataElasticService = new CognitiveDataElasticService(_logger, iElasticConnector, iElasticIndexBuilder, _liteXStorageProviderFactory);
                IModelIngestService modelIngestService = new ModelIngestService(_logger);
                ModelIngest modelIngest = new ModelIngest(_logger, appConfig, modelIngestService, iElasticConnector);

                rssFeed = new RssFeed(_logger, appConfig, iElasticIndexBuilder, iElasticConnector, _iUtilityFunctions);
                fileIngestion = new FileIngestion(_logger, appConfig, iElasticIndexBuilder, iElasticConnector, _liteXStorageProviderFactory);
                var jobDetail = appConfig.JobDeatil.Where(x => x.Id == jobId).FirstOrDefault();
                JobProcessType processType = new JobProcessType();

                if (jobDetail != null)
                {
                    processType = jobDetail.CognitiveProcessType;
                    GetFeatureConfiguration(featureConfigurationFactory, jobClientFeatureMappingFactory, JobTrackingFactory, processType, jobId,
                         _logger, appConfig, iCognitiveDataElasticService, iClientConfigFactory, modelIngest, jobHistoryFactory, jobDetail);
                }
                System.Threading.Thread.Sleep(500);
                iCognitiveConfiguration = null;
                iElasticConnector = null;
                iElasticIndexBuilder = null;
                iCognitiveDataElasticService = null;
                //cognitiveDataIngest = null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[NER] Initial_Validation - job id : {jobId}, ingest exception, {ex.Message}, {ex.StackTrace}");
            }
            return Task.CompletedTask;
        }

        private void GetFeatureConfiguration(IMongoConfigFactory featureConfigurationFactory,
            IMongoConfigFactory jobClientFeatureMappingFactory,
            IMongoConfigFactory JobTrackingFactory, JobProcessType processType, string jobId,
            ILogger<dynamic> logger, ApplicationConfig appConfig,
            ICognitiveDataElasticService cognitiveDataElasticService,
            IMongoConfigFactory clientConfigFactory, ModelIngest modelIngest, IMongoConfigFactory jobHistoryFactory, JobDeatil jobDetail)
        {
            var featureConfigItem = featureConfigurationFactory.GetById(jobId);
            var jobClientFeatureMapping = jobClientFeatureMappingFactory.GetById(jobId);
            var clientIds = BsonSerializer.Deserialize<JobClientFeatureMapping>(jobClientFeatureMapping)?.clientIds;

            var jobTrackings = JobTrackingFactory.GetByIds(clientIds?.ToArray());

            if (appConfig.TenantType.Equals(TenantConnectionType.Multi.ToString()) &&
                (ApplicationConfig.TenantCollections == null || ApplicationConfig.TenantCollections.Count() == 0))
            {

                TenantData tenantData = new TenantData(_logger, _iTenantService);
                tenantData.TenantDataPrepare(jobId, TenantConnectionType.Multi, appConfig);
            }

            foreach (var jobTracking in jobTrackings)
            {
                JobProcessInfo jobProcessInfo = CommonUtility.GetFeatureConfiguration(processType, jobTracking, featureConfigItem);
                var client = BsonSerializer.Deserialize<JobTracking>(jobTracking);

                if (!string.IsNullOrEmpty(jobProcessInfo.ClientId) && appConfig.AllowedClientList.Contains(jobProcessInfo.ClientId))
                {
                    var jobTrackingUpdateInfo = ProcessJobByProcessType(jobId, processType, logger, appConfig, cognitiveDataElasticService,
                     clientConfigFactory, modelIngest, jobProcessInfo, jobDetail, client, JobTrackingFactory.GetById(client.clientId), JobTrackingFactory, jobHistoryFactory);

                    lock (lockJobTracking)
                    {
                        var dbJobTracking = JobTrackingFactory.GetById(client.clientId);
                        CommonUtility.UpdateJobByProcessType(client, dbJobTracking, processType, jobTrackingUpdateInfo, appConfig, jobHistoryFactory, JobTrackingFactory, "");
                    }
                }

            }
        }

        private JobTrackingUpdateInfo ProcessJobByProcessType(string jobId, JobProcessType processType, ILogger<dynamic> logger, ApplicationConfig appConfig,
             ICognitiveDataElasticService cognitiveDataElasticService, IMongoConfigFactory clientConfigFactory, ModelIngest modelIngest, JobProcessInfo jobProcessInfo, JobDeatil jobDetail,
             JobTracking client, dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            var jobTrackingUpdateInfo = new JobTrackingUpdateInfo();
            switch (processType)
            {
                case JobProcessType.NEREntities:
                    {
                        NERDataIngest cognitiveDataIngest = new NERDataIngest(logger, appConfig, cognitiveDataElasticService, _iUtilityFunctions);
                        jobTrackingUpdateInfo = cognitiveDataIngest.GenerateNERData(jobId, JobProcessType.NEREntities, jobProcessInfo, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);

                        cognitiveDataIngest = null;
                    }
                    break;
                case JobProcessType.DataDictionary:
                    {
                        DataDictionaryIngest dataIngest = new DataDictionaryIngest(logger, appConfig, cognitiveDataElasticService, clientConfigFactory, _iUtilityFunctions);
                        jobTrackingUpdateInfo = dataIngest.GenerateDataDictionaryForClients(jobId, jobProcessInfo, jobDetail, appConfig, JobProcessType.DataDictionary, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                        dataIngest = null;
                    }
                    break;
                case JobProcessType.AutoSuggestion:
                    {
                        NERDataIngest cognitiveDataIngest = new NERDataIngest(logger, appConfig, cognitiveDataElasticService, _iUtilityFunctions);
                        jobTrackingUpdateInfo = cognitiveDataIngest.GenerateNERData(jobId, JobProcessType.AutoSuggestion, jobProcessInfo, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                        cognitiveDataIngest = null;
                    }
                    break;
                case JobProcessType.RelatedSearch:
                    {
                        jobTrackingUpdateInfo = modelIngest.GenerateModelFiles(jobId, ModelType.RelatedSearch, jobProcessInfo, _iUtilityFunctions, appConfig, JobProcessType.RelatedSearch, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                    }
                    break;
                case JobProcessType.PeopleAlsoSearch:
                    {
                        jobTrackingUpdateInfo = modelIngest.GenerateModelFiles(jobId, ModelType.PeopleAlsoSearch, jobProcessInfo, _iUtilityFunctions, appConfig, JobProcessType.RelatedSearch, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                    }
                    break;
                case JobProcessType.NERCustomEntities:
                    {
                        NERDataIngest cognitiveDataIngest = new NERDataIngest(logger, appConfig, cognitiveDataElasticService, _iUtilityFunctions);
                        jobTrackingUpdateInfo = cognitiveDataIngest.GenerateNERData(jobId, JobProcessType.NERCustomEntities, jobProcessInfo, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                        cognitiveDataIngest = null;
                    }
                    break;

                case JobProcessType.DataSummary:
                    {
                        NERDataIngest cognitiveDataIngest = new NERDataIngest(logger, appConfig, cognitiveDataElasticService, _iUtilityFunctions);
                        jobTrackingUpdateInfo = cognitiveDataIngest.GenerateNERData(jobId, JobProcessType.DataSummary, jobProcessInfo, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                        cognitiveDataIngest = null;
                    }
                    break;

                case JobProcessType.RssFeed:
                    {
                        jobTrackingUpdateInfo = rssFeed.GenerateRssFeed(jobId, jobProcessInfo);
                    }
                    break;
                case JobProcessType.FileIngestion:
                    {

                        jobTrackingUpdateInfo = fileIngestion.IngestFiles(jobId, jobProcessInfo, jobDetail);
                    }
                    break;
                default: break;
            }

            return jobTrackingUpdateInfo;
        }
    }
}

