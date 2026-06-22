using Microsoft.Extensions.Logging;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Elastic;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;
using System;
using System.Linq;

namespace SearchLego.DataSeerer.Integration
{
    public class NERDataIngest
    {
        private readonly ILogger<dynamic> _logger;
        private readonly ApplicationConfig _appConfig;
        private readonly ICognitiveDataElasticService _iCognitiveDataElasticService;
        private readonly IUtilityFunctions _utilityFunctions;

        public NERDataIngest(ILogger<dynamic> logger, ApplicationConfig appConfig, ICognitiveDataElasticService cognitiveDataElasticService,
            IUtilityFunctions utilityFunctions)
        {
            _logger = logger;
            _logger.LogInformation($"Entities and AutoSuggestions Ingest constructor called.");
            _iCognitiveDataElasticService = cognitiveDataElasticService;
            _appConfig = appConfig;
            _utilityFunctions = utilityFunctions;
            _logger.LogInformation($"ElasticSearch connections are established.");

        }

        public JobTrackingUpdateInfo GenerateNERData(string jobId, JobProcessType cognitiveJobProcess, JobProcessInfo jobProcessInfo, JobTracking client,
                                                    dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            IMongoConfigFactory nerEntityRulerFactory = new MongoClientSetting().GetDBObject(_logger, _appConfig, MongoStaticName.NEREntityRuler);
            var jobtrackingUpdateInfo = new JobTrackingUpdateInfo();
            var success = false;
            try
            {
                var jobDetail = _appConfig.JobDeatil.Where(w => w.Id == jobId).FirstOrDefault();

                switch (cognitiveJobProcess)
                {
                    case JobProcessType.NEREntities:
                    case JobProcessType.NERCustomEntities:
                        success = _iCognitiveDataElasticService.FetchAndUpdateNEREntities(jobDetail, jobProcessInfo, _appConfig, _utilityFunctions, jobtrackingUpdateInfo, nerEntityRulerFactory, cognitiveJobProcess, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                        break;
                    case JobProcessType.AutoSuggestion:
                        success = _iCognitiveDataElasticService.FetchAndUpdateAutoSuggestions(jobDetail, jobProcessInfo, _appConfig, _utilityFunctions, jobtrackingUpdateInfo, cognitiveJobProcess, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                        break;
                    case JobProcessType.DataSummary:
                        success = _iCognitiveDataElasticService.GenerateDataSummary(jobDetail, jobProcessInfo, _appConfig, _utilityFunctions, jobtrackingUpdateInfo, cognitiveJobProcess, client, dbJobTracking, jobHistoryFactory, JobTrackingFactory);
                        break;
                    default:
                        break;
                }
                jobtrackingUpdateInfo.SkipUpdate = !success;
                jobtrackingUpdateInfo.Status = true;
            }
            catch (Exception ex)
            {
                _logger.LogError("[NER] Initial_Validation - Error occured while updating NER data for job id: " + jobId + "Error Message :" + ex.Message);
                jobtrackingUpdateInfo.Status = false;
            }

            return jobtrackingUpdateInfo;
        }
    }
}
