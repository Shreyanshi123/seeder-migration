using Microsoft.Extensions.Logging;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Elastic;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Entities.Cognitive;
using SearchLego.DataSeeder.Host;
using System;
using System.Collections.Generic;

namespace SearchLego.DataSeerer.Integration
{
    public class DataDictionaryIngest
    {
        private readonly ILogger<dynamic> _logger;
        private readonly ApplicationConfig _appConfig;
        private readonly IMongoConfigFactory _mongoClientConfigFactory;
        private readonly ICognitiveDataElasticService _cognitiveDataElasticService;
        private readonly IUtilityFunctions _utilityFunctions;

        public DataDictionaryIngest(ILogger<dynamic> logger, ApplicationConfig appConfig, ICognitiveDataElasticService cognitiveDataElasticService,
            IMongoConfigFactory mongoClientConfigFactory, IUtilityFunctions utilityFunctions)
        {
            _logger = logger;
            _logger.LogInformation($"Data Dictionary Ingest constructor called.");
            _appConfig = appConfig;
            _cognitiveDataElasticService = cognitiveDataElasticService;
            _mongoClientConfigFactory = mongoClientConfigFactory;
            _utilityFunctions = utilityFunctions;
        }
        public JobTrackingUpdateInfo GenerateDataDictionaryForClients(string jobId, JobProcessInfo jobProcessInfo,JobDeatil jobDetail,ApplicationConfig appConfig, JobProcessType processType, 
            JobTracking client, dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            var jobTrackingUpdateInfo = new JobTrackingUpdateInfo();
            try
            {
                string indexPrefix = "";
                if(jobDetail != null)
                {
                    indexPrefix = !string.IsNullOrEmpty(jobDetail.IndexPrefix) ? jobDetail.IndexPrefix : "";
                }
                var baseIndexName = indexPrefix + "_" + Constants.DATA_DICTIONARY + "_" + jobProcessInfo.ClientId;
                if (!string.IsNullOrEmpty(indexPrefix))
                {
                    if (CommonUtility.IsExecutionRequiredForClient(jobProcessInfo, Constants.DICTIONARY))
                    {
                        _logger.LogInformation($"Data Dictionary Job is started for {jobProcessInfo.ClientId}");
                        var resp = _cognitiveDataElasticService.DataDictionaryIngestClientWise(jobProcessInfo, _appConfig, baseIndexName, _utilityFunctions, jobTrackingUpdateInfo, client,
                         dbJobTracking, processType, jobHistoryFactory, JobTrackingFactory); 
                        if (resp.IsSuccess)
                        {
                            _logger.LogInformation($"Data Dictionary Job is cognitive response for {jobProcessInfo.ClientId} is sucess");
                            var activeIndexesForClient = new List<string>();
                            var isSuccessful = UpdateDataDictionaryInfoToMongo(resp, activeIndexesForClient, jobProcessInfo);
                            if (isSuccessful)
                            {
                                _cognitiveDataElasticService.DeleteOldDataDictionaryIndexes(baseIndexName, activeIndexesForClient);
                                _logger.LogInformation($"Old Data Dictionary Indexes is deleted Successfully for {jobProcessInfo.ClientId}");
                            }
                            jobTrackingUpdateInfo.Status = resp.IsSuccess;
                            jobTrackingUpdateInfo.PreviousIndex = jobProcessInfo.ClientAdditionalSetting.currentIndex;
                            jobTrackingUpdateInfo.CurrentIndex = resp.IndexName;
                            _logger.LogInformation($" Data Dictionary job run  Successfully for {jobProcessInfo.ClientId}");
                        }
                        else
                        {
                            jobTrackingUpdateInfo.SkipUpdate = true;
                        }
                    }
                    else
                    {
                        jobTrackingUpdateInfo.SkipUpdate = true;
                    }
                }
                else
                {
                    _logger.LogError($"[Data Dictionary] Initial_Validation - IndexPrefix is not found for {jobProcessInfo.ClientId} and  job id: " + jobId);
                }
               
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Data Dictionary] Initial_Validation - Error occured while updating DataDictionary data for {jobProcessInfo.ClientId} and  job id: " + jobId + "Error Message :" + ex.Message);
            }

            return jobTrackingUpdateInfo;
        }

        private bool UpdateDataDictionaryInfoToMongo(DataDictionaryResponse dataDictionaryResponse, List<string> activeIndexesForClient, JobProcessInfo jobProcessInfo)
        {
            var isSuccess = false;
            if (dataDictionaryResponse.IsSuccess)
            {
                activeIndexesForClient.Add(dataDictionaryResponse.IndexName);
                activeIndexesForClient.Add(jobProcessInfo.ClientAdditionalSetting.currentIndex);

                #region update client config
                var clientConfig = _mongoClientConfigFactory.GetById(jobProcessInfo.ClientId);
                if (clientConfig != null)
                {
                    clientConfig["data_dictionary_index"] = dataDictionaryResponse.IndexName;
                    _mongoClientConfigFactory.Update(clientConfig);
                    _logger.LogInformation($"Data Dictionary Index is updated in mongo successfully for {jobProcessInfo.ClientId}");
                }
                #endregion

                #region update Crawl (JobTracking in mongo)

                //var history_crawl = new CrawlHistory()
                //{
                //    ExecutionTime = DateTime.Now.ToString(),
                //    CrawlType = CrawlType.Full,
                //    RecordsUpdated = dataDictionaryResponse.TotalRecords
                //};


                //var crawlSetting = _clientCrawlSetting.GetSettingById(_mongoClientCrawlFactory, baseIndexNameForClient);
                //if (crawlSetting != null)
                //{
                //    // add to skip list for deletion
                //    activeIndexesForClient.Add(crawlSetting.DataDictionary.CurrentIndex);

                //    crawlSetting.DataDictionary = crawlSetting.DataDictionary ?? new DataDictionary();
                //    crawlSetting.LastUpdatedDate = DateTime.Now.ToString();
                //    crawlSetting.ClientId = cognitiveSettingsConfig.clientId;
                //    crawlSetting.DataDictionary.PreviousIndex = crawlSetting.DataDictionary.CurrentIndex;
                //    crawlSetting.DataDictionary.CurrentIndex = dataDictionaryResponse.IndexName;
                //    crawlSetting.DataDictionary.LastUpdated = DateTime.Now.ToString();
                //    crawlSetting.IsForcedToFullCrawl = false;
                //    if (crawlSetting.CrawlHistory == null)
                //    {
                //        crawlSetting.CrawlHistory = new List<CrawlHistory>() { history_crawl };
                //    }
                //    else
                //    {
                //        crawlSetting.CrawlHistory.Insert(0, history_crawl);
                //    }
                //    _clientCrawlSetting.UpdateSetting(_mongoClientCrawlFactory, crawlSetting); //update
                //}
                //else
                //{
                //    //create
                //    crawlSetting = new CrawlSetting()
                //    {
                //        _id = baseIndexNameForClient,
                //        ClientId = cognitiveSettingsConfig.clientId,
                //        LastUpdatedDate = DateTime.Now.ToString(),
                //        IndexName = dataDictionaryResponse.IndexName,
                //        IndexType = jobDetail.IndexType,
                //        IsForcedToFullCrawl = false,
                //        DataDictionary = new DataDictionary
                //        {
                //            CurrentIndex = dataDictionaryResponse.IndexName,
                //            PreviousIndex = dataDictionaryResponse.IndexName,
                //            LastUpdated = DateTime.Now.ToString()
                //        },
                //        CrawlHistory = new List<CrawlHistory>() { history_crawl }
                //    };
                //    _clientCrawlSetting.AddSetting(_mongoClientCrawlFactory, crawlSetting);
                //}
                #endregion
                isSuccess = true;
            }
            return isSuccess;
        }
    }
}
