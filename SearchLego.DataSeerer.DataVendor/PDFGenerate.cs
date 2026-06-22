using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Elastic;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using System;
using System.Linq;

namespace SearchLego.DataSeerer.Integration
{
    public class PDFGenerate
    {
        private readonly ILogger<dynamic> _logger;
        private readonly ApplicationConfig _jobsDetail;
        private readonly IMongoConfigFactory _previewJobConfigFactory;
        private readonly IElasticIngest _iElasticIngest;
        public PDFGenerate(ILogger<dynamic> logger, ApplicationConfig jobsDetail, IElasticIngest iElasticInges, IMongoConfigFactory previewJobConfigFactory)
        {
            _previewJobConfigFactory = previewJobConfigFactory;
            _iElasticIngest = iElasticInges;
            _jobsDetail = jobsDetail;
            _logger = logger;
        }
        public JobTrackingUpdateInfo GeneratePDF(string jobId,JobProcessInfo jobProcessInfo ,ApplicationConfig appConfig, IUtilityFunctions utilityFunctions)
        {
            var jobTrackingUpdateInfoResult = new JobTrackingUpdateInfo();
            try
            {
              
                string indexName = string.Empty;
                // get index config data from mongo 
                var item = _previewJobConfigFactory.GetById(jobId);
                if (item != null)
                {
                    var previewJobConfig = BsonSerializer.Deserialize<FeatureConfiguration>(item);
                    var indexTypes = jobProcessInfo?.FeatureConfigurationSetting?.indexTypes;
                    if (indexTypes != null)
                    {
                        foreach (var indexType in indexTypes)
                        {
                            if (indexType != null)
                            {
                                indexName = utilityFunctions.GetIndexName(jobProcessInfo, appConfig, indexType);
                                jobTrackingUpdateInfoResult.Status = _iElasticIngest.GeneratePDFUpdateDocContentPageWise(_jobsDetail.JobDeatil.Where(w => w.Id == jobId).FirstOrDefault(), _jobsDetail, indexName, jobProcessInfo.isForcedExecution, _previewJobConfigFactory);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Initial_Validation - Error occured while generating pdf and all  for job id: " + jobId + "Error Message :" + ex.Message);
            }
            return jobTrackingUpdateInfoResult;
        }
    }
}
