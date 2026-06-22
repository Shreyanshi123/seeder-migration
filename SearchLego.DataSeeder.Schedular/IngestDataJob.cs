

using LiteX.Storage.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Connector;
using SearchLego.DataSeeder.Elastic;
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
    public class IngestDataJob : IJob
    {
        private readonly ILogger<dynamic> _logger;
        private readonly IConfiguration _config;
        private readonly ApplicationConfig jobsDetail;
        private readonly ISLBConfig _iSLBConfig;
        private readonly ISettingValue _iSettingValue;
        private readonly IUtilityFunctions _iUtilityFunctions;
        private readonly IClientCrawlSetting _clientCrawlSetting;
        private readonly IExtractTextFromFile _extractTextFromFile;
        private readonly IConvertFileToPDF _convertFileToPDF;
        private readonly ILiteXStorageProviderFactory _liteXStorageProviderFactory;
        private readonly ITenantService _iTenantService;


        public IngestDataJob(ILogger<dynamic> logger, IConfiguration config, ISLBConfig iSLBConfig, ISettingValue iSettingValue, IUtilityFunctions iUtilityFunctions,
            IClientCrawlSetting clientCrawlSetting, IExtractTextFromFile extractTextFromFile, IConvertFileToPDF convertFileToPDF, ILiteXStorageProviderFactory liteXStorageProviderFactory,
            ITenantService tenantService)
        {
            _logger = logger;
            _config = config;
            _logger.LogInformation($"IngestDataJob constructor called.");
            jobsDetail = _config.GetSection("jobs").Get<IList<ApplicationConfig>>().FirstOrDefault();
            _iSLBConfig = iSLBConfig;
            _iSettingValue = iSettingValue;
            _iUtilityFunctions = iUtilityFunctions;
            _clientCrawlSetting = clientCrawlSetting;
            _convertFileToPDF = convertFileToPDF;
            _extractTextFromFile = extractTextFromFile;
            _liteXStorageProviderFactory = liteXStorageProviderFactory;
            _iTenantService = tenantService;
            _logger.LogInformation($"ElasticSearch and SQL Server connections are established.");
        }
        /// <summary>
        /// Job Execution on scheduled time interval
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task Execute(IJobExecutionContext context)
        {
            var jobId = context.JobDetail.Description;
            try
            {

                IDataSourceFactory iDataSourceFactory = new DataSourceFactory(_logger).GetDBObject(jobsDetail);
                IMongoConfigFactory iMongoConfigFactory = new MongoConfigFactory().GetDBObject(_logger, jobsDetail, MongoStaticName.Configuration);
                IMongoConfigFactory iMongoConfigurationFactory = new MongoClientSetting().GetDBObject(_logger, jobsDetail, MongoStaticName.FeatureConfiguration);
                IMongoConfigFactory iMongoClientCrawlFactory = new MongoClientSetting().GetDBObject(_logger, jobsDetail, MongoStaticName.ClientSetting);
                IMongoConfigFactory iMongoClientConfig = new MongoConfigFactory().GetDBObject(_logger, jobsDetail, MongoStaticName.ClientConfiguration);
                IElasticConnector iElasticConnector = new ElasticConnector(_logger, jobsDetail);
                iElasticConnector.ElasticConnect();
                IElasticIndexBuilder iElasticIndexBuilder = new ElasticIndexBuilder(iElasticConnector);
                IElasticIngest iElasticIngest = new ElasticIngest(_logger, iElasticConnector, iElasticIndexBuilder, _extractTextFromFile, _convertFileToPDF, _liteXStorageProviderFactory);
                IngestData ingestData = new IngestData(_logger, jobsDetail, iElasticIndexBuilder, iElasticIngest, iDataSourceFactory,
                    iMongoConfigFactory, _iSLBConfig, _iSettingValue, _iUtilityFunctions, iMongoClientCrawlFactory, _clientCrawlSetting, iMongoClientConfig, _iTenantService, iMongoConfigurationFactory);
                ingestData.Ingest(jobId);
                System.Threading.Thread.Sleep(500);
                iDataSourceFactory = null;
                iMongoConfigFactory = null;
                iMongoClientCrawlFactory = null;
                iElasticConnector = null;
                iElasticIngest = null;
                ingestData = null;


            }
            catch (Exception ex)
            {
                _logger.LogError($"Initial_Validation - job id : {jobId}, ingest exception, {ex.Message}, {ex.StackTrace}");
            }

            return Task.CompletedTask;
        }
    }


}
