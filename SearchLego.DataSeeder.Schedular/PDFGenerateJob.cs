using LiteX.Storage.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization;
using Quartz;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Connector;
using SearchLego.DataSeeder.Elastic;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.FileConvert;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;
using SearchLego.DataSeerer.Integration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.Schedular
{
    [DisallowConcurrentExecution]
    public class PDFGenerateJob : IJob
    {
        private readonly ILogger<dynamic> _logger;
        private readonly IConfiguration _config;
        private readonly ApplicationConfig jobsDetail;
        private readonly IExtractTextFromFile _extractTextFromFile;
        private readonly IConvertFileToPDF _convertFileToPDF;
        private readonly ILiteXStorageProviderFactory _liteXStorageProviderFactory;
        private readonly IUtilityFunctions _iUtilityFunctions;
        private readonly ApplicationConfig appConfig;
        private readonly static Object lockJobTracking = new Object();
        private readonly ITenantService _iTenantService;
        public PDFGenerateJob(ILogger<dynamic> logger, IConfiguration config, IExtractTextFromFile extractTextFromFile,
            IConvertFileToPDF convertFileToPDF, ILiteXStorageProviderFactory liteXStorageProviderFactory, IUtilityFunctions utilityFunctions,
            ITenantService iTenantService)
        {
            _logger = logger;
            _config = config;
            _logger.LogInformation($"IngestDataJob constructor called.");
            jobsDetail = _config.GetSection("jobs").Get<IList<ApplicationConfig>>().FirstOrDefault();
            _convertFileToPDF = convertFileToPDF;
            _extractTextFromFile = extractTextFromFile;
            _liteXStorageProviderFactory = liteXStorageProviderFactory;
            _iUtilityFunctions = utilityFunctions;
            appConfig = _config.GetSection("jobs").Get<IList<ApplicationConfig>>().FirstOrDefault();
            _logger.LogInformation($"ElasticSearch and SQL Server connections are established.");

            _iTenantService = iTenantService;

        }
        public Task Execute(IJobExecutionContext context)
        {
            var jobId = context.JobDetail.Description;
            try
            {
                IMongoConfigFactory iJobTrackingFactory = new MongoClientSetting().GetDBObject(_logger, jobsDetail, MongoStaticName.JobTracking);
                IMongoConfigFactory iFeatureConfigurationFactory = new MongoClientSetting().GetDBObject(_logger, jobsDetail, MongoStaticName.FeatureConfiguration);
                IMongoConfigFactory jobClientFeatureMappingFactory = new MongoClientSetting().GetDBObject(_logger, jobsDetail, MongoStaticName.JobClientFeatureMapping);
                IMongoConfigFactory jobHistoryFactory = new MongoClientSetting().GetDBObject(_logger, appConfig, MongoStaticName.JobHistory);
                IElasticConnector iElasticConnector = new ElasticConnector(_logger, jobsDetail);
                iElasticConnector.ElasticConnect();
                IElasticIndexBuilder iElasticIndexBuilder = new ElasticIndexBuilder(iElasticConnector);
                IElasticIngest iElasticIngest = new ElasticIngest(_logger, iElasticConnector, iElasticIndexBuilder, _extractTextFromFile, _convertFileToPDF, _liteXStorageProviderFactory);
                var item = iFeatureConfigurationFactory.GetById(jobId);
                var featureConfiguration = BsonSerializer.Deserialize<FeatureConfiguration>(item);
                var jobClientFeatureMapping = jobClientFeatureMappingFactory.GetById(jobId);
                var clientIds = BsonSerializer.Deserialize<JobClientFeatureMapping>(jobClientFeatureMapping)?.clientIds;
                var jobTrackings = iJobTrackingFactory.GetByIds(clientIds?.ToArray());


                if (appConfig.TenantType.Equals(TenantConnectionType.Multi.ToString()) &&
                (ApplicationConfig.TenantCollections == null || ApplicationConfig.TenantCollections.Count() == 0))
                {
                    TenantData tenantData = new TenantData(_logger, _iTenantService);
                    tenantData.TenantDataPrepare(jobId, TenantConnectionType.Multi, appConfig);
                }


                foreach (var jobTracking in jobTrackings)
                {
                    var client = BsonSerializer.Deserialize<JobTracking>(jobTracking);
                    JobProcessInfo jobProcessInfo = CommonUtility.GetFeatureConfiguration(JobProcessType.PDFGenerate, jobTracking, item);
                    if (!string.IsNullOrEmpty(jobProcessInfo.ClientId) && appConfig.AllowedClientList.Contains(jobProcessInfo.ClientId))
                    {
                        PDFGenerate pdfGenerate = new PDFGenerate(_logger, jobsDetail, iElasticIngest, iFeatureConfigurationFactory);
                        JobTrackingUpdateInfo jobTrackingUpdateInfo = pdfGenerate.GeneratePDF(jobId, jobProcessInfo, appConfig, _iUtilityFunctions);
                        lock (lockJobTracking)
                        {
                            var dbJobTracking = iJobTrackingFactory.GetById(client.clientId);
                            CommonUtility.UpdateJobByProcessType(client, dbJobTracking, JobProcessType.PDFGenerate, jobTrackingUpdateInfo, appConfig, jobHistoryFactory, iJobTrackingFactory,"");
                        }
                    }
                }
                System.Threading.Thread.Sleep(500);
                iJobTrackingFactory = null;
                iElasticConnector = null;
                iElasticIndexBuilder = null;
                iElasticIngest = null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Initial_Validation - job id : {jobId}, ingest exception, {ex.Message}, {ex.StackTrace}");
            }
            return Task.CompletedTask;
        }
    }
}
