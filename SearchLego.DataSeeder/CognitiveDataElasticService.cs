using Elasticsearch.Net;
using KvpbaseSDK;
using LiteX.Storage.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Nest;
using Newtonsoft.Json;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Entities.Cognitive;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.NER;
using System;
using System.Collections.Generic;
using System.Linq;
using N = Newtonsoft.Json.Linq;

namespace SearchLego.DataSeeder.Elastic
{
    public class CognitiveDataElasticService : ICognitiveDataElasticService
    {
        private readonly IElasticConnector _iElasticConnector;
        private readonly IElasticIndexBuilder _iElasticIndexBuilder;
        private readonly ILogger<dynamic> _logger;
        private readonly ILiteXStorageProviderFactory _liteXStorageProviderFactory;
        private readonly IMongoConfigFactory _mongoClientCrawlFactory;
        public CognitiveDataElasticService(ILogger<dynamic> logger, IElasticConnector iElasticConnector,
            IElasticIndexBuilder iElasticIndexBuilder, ILiteXStorageProviderFactory liteXStorageProviderFactory)
        {
            _iElasticConnector = iElasticConnector;
            _iElasticIndexBuilder = iElasticIndexBuilder;
            _logger = logger;
            _liteXStorageProviderFactory = liteXStorageProviderFactory;

        }


        public bool FetchAndUpdateNEREntities(JobDeatil jobDetail, JobProcessInfo jobProcessInfo, ApplicationConfig appConfig, IUtilityFunctions utilityFunctions,
            JobTrackingUpdateInfo jobTrackingUpdateInfo, IMongoConfigFactory nerEntityRulerFactory, JobProcessType processType, JobTracking client_jt, dynamic dbJobTracking,
            IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            var success = false;
            var inittime = DateTime.Now;
            dynamic searchResult;
            var indexTypes = jobProcessInfo?.FeatureConfigurationSetting?.indexTypes;

            if (indexTypes != null)
            {
                foreach (var indexType in indexTypes)
                {
                    int take = indexType.batchSize > 0 ? indexType.batchSize : jobDetail.IngestBatchSize;
                    var includeFields = indexType.includeFields.Count > 0 ? indexType.includeFields : null;

                    string indexName = utilityFunctions.GetIndexName(jobProcessInfo, appConfig, indexType);

                    if (indexName != null)
                    {
                        if (JobProcessType.NERCustomEntities == processType)
                        {
                            var nerEntityRulerData = nerEntityRulerFactory.GetCustomerProcessedData();

                            foreach (var entity in nerEntityRulerData)
                            {
                                NerEntityRuler client = BsonSerializer.Deserialize<NerEntityRuler>(entity);
                                string data = client.pattern;

                                searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                                {
                                    _source = new { },
                                    Size = take,
                                    query = new
                                    {
                                        multi_match = new
                                        {
                                            query = data,
                                            fields = includeFields
                                        }
                                    }
                                }));

                                dynamic hitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                                IEnumerable<dynamic> _iHits = ((IEnumerable<dynamic>)hitsResult.hits).Cast<dynamic>();
                                IEnumerable<dynamic> list_hits = _iHits;

                                if (_iHits.Any())
                                {
                                    list_hits = _iHits.Select(x => x._source);
                                    NerEntityProcess(jobProcessInfo, jobTrackingUpdateInfo, processType, ref success, inittime, searchResult, indexType, includeFields, indexName, ref hitsResult, list_hits,
                                                    jobProcessInfo.ClientId, appConfig, client_jt, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                                    if (success)
                                    {
                                        entity["isCustomNerProcessed"] = true;
                                        nerEntityRulerFactory.UpdateNerEntityRuler(entity);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (jobProcessInfo.isForcedExecution)
                            {
                                _iElasticConnector.ElasticConnect(indexName);

                                bool res = CommonUtility.UpdateStatusAsInitial(Constants.IS_NER_PROCESSED, _iElasticConnector._elasticClient);
                                if (res)
                                {
                                    _logger.LogInformation($"NER Jobs is set for Initial State for {indexName} ");
                                }
                            }
                            _logger.LogInformation($"NER Jobs is started for {indexName} ");
                            try
                            {
                                string filePath = string.Empty;
                                searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                                {
                                    _source = new { },
                                    Size = take,
                                    query = new
                                    {
                                        match = new
                                        {
                                            isNERProcessed = new { query = Constants.INITIAL_STATE }
                                        }
                                    }
                                }));


                                dynamic hitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                                // Added for Run the Failhits once
                                dynamic failHitsResult = null;
                                if (hitsResult != null && hitsResult.hits.Count == 0)
                                {
                                    searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                                    {
                                        _source = new { },
                                        Size = take,
                                        query = new
                                        {
                                            match = new
                                            {
                                                isNERProcessed = new { query = Constants.INCOMPLETE }
                                            }
                                        }
                                    }));
                                    failHitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                                }
                                if (hitsResult != null)
                                {
                                    IEnumerable<dynamic> _iHits = ((IEnumerable<dynamic>)hitsResult.hits).Cast<dynamic>();
                                    IEnumerable<dynamic> list_hits = _iHits;

                                    if (_iHits.Any())
                                    {
                                        list_hits = _iHits.Select(x => x._source);
                                    }
                                    else
                                    {
                                        var _iFailHits = ((IEnumerable<dynamic>)failHitsResult.hits).Cast<dynamic>();
                                        list_hits = _iFailHits.Select(x => x._source);
                                    }
                                    // Ended
                                    if ((hitsResult != null && hitsResult.hits.Count > 0) || (failHitsResult != null && failHitsResult.hits.Count > 0))
                                        NerEntityProcess(jobProcessInfo, jobTrackingUpdateInfo, processType, ref success, inittime, searchResult, indexType, includeFields, indexName, ref hitsResult,
                                            list_hits, jobProcessInfo.ClientId, appConfig, client_jt, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"{indexName}:[NER] Initial_Validation - Exception occured while updating index - {indexName},  {ex.Message}");
                            }
                        }

                    }
                }
            }
            return success;
        }

        private void NerEntityProcess(JobProcessInfo jobProcessInfo, JobTrackingUpdateInfo jobTrackingUpdateInfo, JobProcessType processType, ref bool success, DateTime inittime,
            dynamic searchResult, IndexType indexType, List<string> includeFields, string indexName, ref dynamic hitsResult, IEnumerable<dynamic> list_hits, string clienId,
            ApplicationConfig appConfig, JobTracking client, dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            _logger.LogInformation($"NER Entities job has started for {indexName} for {hitsResult.hits.Count} records!");
            try
            {
                hitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                var source = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(list_hits));
                _logger.LogInformation($"Ner Jobs for {indexName} Calling the Paython Api");
                Dictionary<string, NER_Entities> nerBulkData = GetBulkNEREntities(source, includeFields, jobProcessInfo.FeatureConfigurationSetting?.baseApi, clienId);
                _logger.LogInformation($"Ner Jobs for {indexName} Completed the Paython Api");
                //source = null;
                hitsResult = null;
                if (nerBulkData != null)
                {
                    _logger.LogInformation($"Ner Jobs for {indexName}  Paython Api is returning data");
                    success = nerBulkData.Keys.Count > 0 ? true : false;
                    jobTrackingUpdateInfo.NoOfUpdatedRecords = nerBulkData.Keys.Count;
                    List<dynamic> elRecords = new List<dynamic>();
                    foreach (var record in nerBulkData)
                    {
                        Dictionary<string, object> doc = new Dictionary<string, object>();
                        doc[Constants.IS_NER_PROCESSED] = Constants.PROCESSED;
                        doc[Constants.NER_ENTITES] = record.Value;
                        elRecords.Add(new { update = new { _index = indexName, _id = record.Key } });
                        elRecords.Add(new { doc = doc, doc_as_upsert = false });
                        //var result = _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(elRecords));
                    }
                    var result = _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(elRecords));

                }
                else
                {
                    string incFields = Constants.IS_NER_PROCESSED;
                    Dictionary<string, object> doc = new Dictionary<string, object>();
                    List<dynamic> elRecords = new List<dynamic>();
                    var fields = incFields.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    List<Dictionary<string, object>> autoRequestData = source.Select(item => item.Where(x =>
                    fields.Any(f => f.ToLower() == x.Key.ToLower()) || x.Key.ToLower() == "id")
                    .ToDictionary(i => i.Key, i => i.Value)
                    ).ToList();

                    foreach (var item in autoRequestData)
                    {
                        string status = Convert.ToString(item.FirstOrDefault(x => x.Key == Constants.IS_NER_PROCESSED).Value);
                        if (status == Constants.INCOMPLETE)
                        {
                            doc[Constants.IS_NER_PROCESSED] = Constants.UNPROCESSED;
                        }
                        else
                        {
                            doc[Constants.IS_NER_PROCESSED] = Constants.INCOMPLETE;
                        }
                        var id = item.FirstOrDefault(x => x.Key == "id").Value;
                        elRecords.Add(new { update = new { _index = indexName, _id = id } });
                        elRecords.Add(new { doc = doc, doc_as_upsert = true });
                    }
                    _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(elRecords));
                    _logger.LogInformation($"NER Entities job has completed for {indexName} for {indexType.batchSize} records!");

                }
                jobTrackingUpdateInfo.Status = success;
                string nerJobResponse = JsonConvert.SerializeObject(nerBulkData);

                CommonUtility.UpdateJobByProcessType(client, dbJobTracking, processType, jobTrackingUpdateInfo, appConfig, jobHistoryFactory, JobTrackingFactory, nerJobResponse);

            }
            catch (Exception ex)
            {
                _logger.LogError($"{indexName}:[NER] Initial_Validation - Exception occured while processing cognitive response {Environment.NewLine} Exception : {ex.Message} {Environment.NewLine} {ex.StackTrace}");
            }
            var timeelapsed = inittime - DateTime.Now;
        }

        public bool FetchAndUpdateAutoSuggestions(JobDeatil jobDetail, JobProcessInfo jobProcessInfo, ApplicationConfig appConfig, IUtilityFunctions utilityFunctions, JobTrackingUpdateInfo jobTrackingUpdateInfo,
            JobProcessType processType, JobTracking client_jt, dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            var success = false;
            var indexTypes = jobProcessInfo?.FeatureConfigurationSetting?.indexTypes;
            if (indexTypes != null)
            {
                foreach (var indexType in indexTypes)
                {
                    var includeFields = indexType.includeFields.Count > 0 ? indexType.includeFields : null;

                    string indexName = utilityFunctions.GetIndexName(jobProcessInfo, appConfig, indexType);
                    if (jobProcessInfo.isForcedExecution)
                    {
                        _iElasticConnector.ElasticConnect(indexName);

                        bool res = CommonUtility.UpdateStatusAsInitial(Constants.IS_AUTO_SUGGESTED, _iElasticConnector._elasticClient);
                        if (res)
                        {
                            _logger.LogInformation($"Auto Suggestion Jobs is set for Initial State for {indexName} ");
                        }
                    }
                    if (indexName != null)
                    {
                        var inittime = DateTime.Now;
                        dynamic searchResult;
                        try
                        {
                            string filePath = string.Empty;

                            searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                            {
                                _source = new { },
                                Size = indexType.batchSize,
                                query = new
                                {
                                    match = new
                                    {
                                        isAutoSuggested = new { query = Constants.INITIAL_STATE }
                                    }
                                }
                            }));

                            dynamic hitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;


                            // Added for Run the Failhits once
                            dynamic failHitsResult = null;
                            if (hitsResult != null && hitsResult.hits.Count == 1)
                            {
                                searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                                {
                                    _source = new { },
                                    Size = indexType.batchSize,
                                    query = new
                                    {
                                        match = new
                                        {
                                            isAutoSuggested = new { query = Constants.INCOMPLETE }
                                        }
                                    }
                                }));
                                failHitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                            }
                            // Ended
                            searchResult = null;
                            string autoSuggestionBulkDataResponse = string.Empty;
                            if ((hitsResult != null && hitsResult.hits.Count > 0) || (failHitsResult != null && failHitsResult.hits.Count > 0))
                            {
                                _logger.LogInformation($"Auto Suggestions job has started for {indexName} for {indexType.batchSize} records!");
                                try
                                {
                                    IEnumerable<dynamic> _iHits = ((IEnumerable<dynamic>)hitsResult.hits).Cast<dynamic>();
                                    IEnumerable<dynamic> list_hits;
                                    if (_iHits.Any())
                                    {
                                        list_hits = _iHits.Select(x => x._source);
                                    }
                                    else
                                    {
                                        var _iFailHits = ((IEnumerable<dynamic>)failHitsResult.hits).Cast<dynamic>();
                                        list_hits = _iFailHits.Select(x => x._source);
                                    }
                                    var source = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(list_hits));
                                    _logger.LogInformation($"Auto-Suggestions Jobs for {indexName} Calling the Paython Api");
                                    Dictionary<string, AutoSuggestion_Entities> autoSuggestionBulkData = GetBulkAutoSuggestionEntities(source, indexType, jobProcessInfo.FeatureConfigurationSetting.baseApi, jobDetail);
                                    _logger.LogInformation($"Auto-Suggestions Jobs for {indexName} Completed the Paython Api");
                                    success = autoSuggestionBulkData.Keys.Count > 0 ? true : false;
                                    UpdateStatus(source, autoSuggestionBulkData, indexName);
                                    jobTrackingUpdateInfo.NoOfUpdatedRecords = autoSuggestionBulkData.Keys.Count;
                                    _logger.LogInformation($"Auto Suggestions job has completed for {indexName} for {indexType.batchSize} records!");

                                    autoSuggestionBulkDataResponse = JsonConvert.SerializeObject(autoSuggestionBulkData);

                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"{indexName}:[Auto_Suggestions] Initial_Validation - Exception occured while processing cognitive response {Environment.NewLine} Exception : {ex.Message} {Environment.NewLine} {ex.StackTrace}");
                                }
                                var timeelapsed = inittime - DateTime.Now;

                            }
                            jobTrackingUpdateInfo.Status = success;
                            CommonUtility.UpdateJobByProcessType(client_jt, dbJobTracking, processType, jobTrackingUpdateInfo, appConfig, jobHistoryFactory, JobTrackingFactory, autoSuggestionBulkDataResponse);

                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"[Auto_Suggestions] Initial_Validation - Exception occured while updating index,  {ex.Message}");
                        }
                        success = success && true;
                    }
                }
            }
            return success;
        }


        // Added for Updatd the Auto-Suggestion Status
        private void UpdateStatus(List<Dictionary<string, object>> source, Dictionary<string, AutoSuggestion_Entities> autoSuggestionBulkData, string indexName)
        {
            List<dynamic> elRecords = new List<dynamic>();
            Dictionary<string, object> doc = new Dictionary<string, object>();
            if (autoSuggestionBulkData.Count == 0)
            {

                string includeFields = Constants.IS_AUTO_SUGGESTED;
                var fields = includeFields.Split(',', StringSplitOptions.RemoveEmptyEntries);
                List<Dictionary<string, object>> autoRequestData = source.Select(item => item.Where(x =>
                fields.Any(f => f.ToLower() == x.Key.ToLower()) || x.Key.ToLower() == "id")
                .ToDictionary(i => i.Key, i => i.Value)
                ).ToList();

                foreach (var item in autoRequestData)
                {
                    string status = Convert.ToString(item.FirstOrDefault(x => x.Key == Constants.IS_AUTO_SUGGESTED).Value);
                    if (status == Constants.INCOMPLETE)
                    {
                        doc[Constants.IS_AUTO_SUGGESTED] = Constants.UNPROCESSED;
                    }
                    else
                    {
                        doc[Constants.IS_AUTO_SUGGESTED] = Constants.INCOMPLETE;
                    }
                    var id = item.FirstOrDefault(x => x.Key == "id").Value;
                    elRecords.Add(new { update = new { _index = indexName, _id = id } });
                    elRecords.Add(new { doc = doc, doc_as_upsert = true });


                }
                _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(elRecords));
            }
            else
            {
                _logger.LogInformation($"Auto-Suggestions Jobs for {indexName} Returning data from Paython Api");
                foreach (var record in autoSuggestionBulkData)
                {

                    Dictionary<string, object> docAutoComplete = new Dictionary<string, object>();

                    //var item = record.Value;
                    //var resultItem = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(item));
                    // var resultItem = JsonConvert.SerializeObject(item);
                    //JArray jArray = JArray.Parse(JsonConvert.SerializeObject(item));
                    //var value = record.Value.SuggestionData.Take(5);

                    string[] splitKey = record.Key.Split(Constants.UNDERSCORE);
                    string[] clientId = indexName.Split(Constants.UNDERSCORE);
                    int i = 1;
                    foreach (var item in record.Value.SuggestionData)
                    {
                        var resultData = item;
                        List<string> FinalList = resultData.ToObject<List<string>>();
                        docAutoComplete[Constants.SUGGESTIONS + Constants.UNDERSCORE + i + Constants.UNDERSCORE + splitKey[0]] = BuildSuggest(FinalList);
                        i++;
                    }
                    doc[Constants.IS_AUTO_SUGGESTED] = Constants.PROCESSED;
                    docAutoComplete[Constants.CLIENT_ID] = clientId[1];
                    docAutoComplete[Constants.PROJECT_ID] = splitKey[0];
                    elRecords.Add(new { update = new { _index = indexName, _id = splitKey[1] } });
                    elRecords.Add(new { doc = doc, doc_as_upsert = true });
                    elRecords.Add(new { update = new { _index = indexName + Constants.AUTO_COMPLETE, _id = splitKey[1] } });
                    elRecords.Add(new { doc = docAutoComplete, doc_as_upsert = true });
                }

                var result = _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(elRecords));




            }
        }

        public Suggestion BuildSuggest(List<string> content)
        {
            return new Suggestion()
            {
                Input = content
            };
        }

        public DataDictionaryResponse DataDictionaryIngestClientWise(JobProcessInfo jobProcessInfo, ApplicationConfig appConfig, string baseIndexName, IUtilityFunctions utilityFunctions,
            JobTrackingUpdateInfo jobTrackingUpdateInfo, JobTracking client, dynamic dbJobTracking, JobProcessType processType, IMongoConfigFactory jobHistoryFactory, IMongoConfigFactory JobTrackingFactory)
        {
            bool isSuccess = false;
            string createdDictionaryIndex = string.Empty;
            int totalRecords = 0;
            int clientRecordsCount = 0;
            try
            {
                var indexTypes = jobProcessInfo?.FeatureConfigurationSetting?.indexTypes;
                //var indexes = cognitiveSettingsConfig.indexes.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var take = 500;
                List<Dictionary<string, object>> clientIndexesData = new List<Dictionary<string, object>>();
                if (indexTypes != null)
                {
                    foreach (var indexType in indexTypes)
                    {
                        var includeFields = indexType.includeFields.Count > 0 ? indexType.includeFields : null;

                        string indexName = utilityFunctions.GetIndexName(jobProcessInfo, appConfig, indexType);
                        _logger.LogInformation($"Data Dictionary Job is started for {jobProcessInfo.ClientId} and {indexName}");
                        if (indexName != null)
                        {
                            var _totaltRespo = JsonConvert.DeserializeObject<dynamic>(
                               _iElasticConnector._lowLevelClient.Count<StringResponse>(indexName, PostData.Serializable(new { })).Body
                               );
                            if (_totaltRespo != null && _totaltRespo.count != null)
                            {
                                _logger.LogInformation($"Data Dictionary for {jobProcessInfo.ClientId} has {_totaltRespo.count} records");

                                totalRecords = (int)_totaltRespo.count;
                                int processedRows = 0;
                                do
                                {
                                    var searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                                    {
                                        _source = new { },
                                        from = processedRows,
                                        Size = take
                                    }));
                                    dynamic hitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                                    if (hitsResult != null && hitsResult.hits.Count > 0)
                                    {
                                        for (int i = 0; i < hitsResult.hits.Count; i++)
                                        {
                                            if (includeFields.Count > 0)
                                            {
                                                var fields = includeFields;
                                                Dictionary<string, object> jobject = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(hitsResult.hits[i]._source));
                                                Dictionary<string, object> requestData = jobject.Where(x => fields.Any(f => f.ToLower() == x.Key.ToLower())).ToDictionary(i => i.Key, i => i.Value);
                                                requestData.Add("project_id", hitsResult.hits[i]._source.projectId);
                                                clientIndexesData.Add(requestData);
                                            }
                                        }
                                        processedRows += hitsResult.hits.Count;
                                        searchResult = null;
                                    }
                                } while (processedRows < totalRecords);
                            }
                        }
                    }

                    clientRecordsCount = clientIndexesData.Count;
                    string data = string.Empty;
                    if (clientRecordsCount > 0)
                    {
                        using (var iNERProcess = new NERProcess())
                        {
                            var request = new
                            {
                                projectid_to_process = jobProcessInfo.ClientAdditionalSetting.projectIds,
                                data = clientIndexesData
                            };
                            _logger.LogInformation($"Data Dictionary Job is - {jobProcessInfo.ClientId} Python Api Started");
                            data = iNERProcess.DataDictionaryProcessRequest(request, jobProcessInfo.FeatureConfigurationSetting.baseApi);
                            _logger.LogInformation($"Data Dictionary Job is - {jobProcessInfo.ClientId} Python Api Completed");
                            clientIndexesData = null;
                        }

                        if (data != null)
                        {
                            _logger.LogInformation($"Data Dictionary Job is - {jobProcessInfo.ClientId} Python Api is returning response");
                            var currentDateTime = DateTime.Now;
                            //var indexPrefix = !string.IsNullOrEmpty(jobDetail.IndexPrefix) ? jobDetail.IndexPrefix + "_" : "";
                            //baseIndexName = indexPrefix + jobDetail.IndexType + "_" + dataDictionaryConfig.clientId;
                            var indexName = baseIndexName + "_" + currentDateTime.Day + "_" +
                                currentDateTime.Month + "_" + currentDateTime.Year + "_" + currentDateTime.Hour + "_" + currentDateTime.Minute;
                            isSuccess = BuildDataDictionaryIndex(data, indexName);
                            if (isSuccess)
                            {
                                createdDictionaryIndex = indexName;
                                jobTrackingUpdateInfo.NoOfUpdatedRecords = clientRecordsCount;
                            }
                            _logger.LogInformation($"Data Dictionary Job   - {jobProcessInfo.ClientId} status:{isSuccess} ");

                        }
                        jobTrackingUpdateInfo.Status = isSuccess;
                        CommonUtility.UpdateJobByProcessType(client, dbJobTracking, processType, jobTrackingUpdateInfo, appConfig, jobHistoryFactory, JobTrackingFactory, data);

                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Data Dictionary] Initial_Validation - Exception occured while updating index,  {ex.Message}");
            }

            return new DataDictionaryResponse { IndexName = createdDictionaryIndex, IsSuccess = isSuccess, TotalRecords = clientRecordsCount };
        }

        public bool DeleteOldDataDictionaryIndexes(string pattern, List<string> activeIndexes)
        {
            var indices = JsonConvert.DeserializeObject<List<dynamic>>(
                    _iElasticConnector._lowLevelClient.Cat.Indices<StringResponse>(
                        new Elasticsearch.Net.Specification.CatApi.CatIndicesRequestParameters()
                        {
                            Format = "json"
                        }).Body
            );
            var indicesToDelete = indices.Where(x =>
            {
                string index = Convert.ToString(x.index);
                return !activeIndexes.Any(y => y == index) && index.Contains(pattern);
            }).Select(x => Convert.ToString(x.index));

            foreach (var index in indicesToDelete)
            {
                _iElasticConnector._elasticClient.Indices.Delete(new DeleteIndexRequest(index));
            }
            return true;
        }

        public bool GenerateDataSummary(JobDeatil jobDetail, JobProcessInfo jobProcessInfo, ApplicationConfig appConfig, IUtilityFunctions utilityFunctions, JobTrackingUpdateInfo jobTrackingUpdateInfo,
            JobProcessType processType, JobTracking client, dynamic dbJobTracking, IMongoConfigFactory jobHistoryFactory, IMongoConfigFactory JobTrackingFactory)
        {
            var success = false;
            var inittime = DateTime.Now;
            dynamic searchResult;
            var indexTypes = jobProcessInfo?.FeatureConfigurationSetting?.indexTypes;

            if (indexTypes != null)
            {
                foreach (var indexType in indexTypes)
                {
                    int take = indexType.batchSize > 0 ? indexType.batchSize : jobDetail.IngestBatchSize;
                    var includeFields = new List<string>(); //indexType.includeFields.Count > 0 ? indexType.includeFields : null;

                    string indexName = utilityFunctions.GetIndexName(jobProcessInfo, appConfig, indexType);

                    if (indexName != null)
                    {
                        if (jobProcessInfo.isForcedExecution)
                        {
                            _iElasticConnector.ElasticConnect(indexName);

                            bool res = CommonUtility.UpdateStatusAsInitial(Constants.IS_DOC_SUMMARY_PROCESSED, _iElasticConnector._elasticClient);
                            if (res)
                            {
                                _logger.LogInformation($"DataSummary Job is set for Initial State for {indexName} ");
                            }
                        }
                        _logger.LogInformation($"DataSummary Job is started for {indexName} ");
                        try
                        {
                            List<dynamic> must = new List<dynamic>();
                            must.Add(new
                            {
                                match = new
                                {
                                    isDocSummaryProcessed = new { query = Constants.INITIAL_STATE }
                                }
                            });
                            must.Add(new
                            {
                                match = new
                                {
                                    isProcessed = new { query = Constants.PROCESSED }
                                }
                            });

                            Dictionary<string, object> bl = new Dictionary<string, object>();
                            bl.Add("bool", new
                            {
                                must = must
                            });

                            string filePath = string.Empty;
                            searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                            {
                                _source = new { },
                                Size = take,
                                query = bl
                            }));
                            dynamic hitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                            // Added for Run the Failhits once
                            dynamic failHitsResult = null;
                            if (hitsResult != null)
                            {
                                if (hitsResult.hits.Count == 0)
                                {
                                    searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                                    {
                                        _source = new { },
                                        Size = take,
                                        query = new
                                        {
                                            match = new
                                            {
                                                isDocSummaryProcessed = new { query = Constants.INCOMPLETE }
                                            }
                                        }
                                    }));
                                    failHitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                                }


                                IEnumerable<dynamic> _iHits = ((IEnumerable<dynamic>)hitsResult.hits).Cast<dynamic>();
                                IEnumerable<dynamic> list_hits = _iHits;

                                if (_iHits.Any())
                                {
                                    list_hits = _iHits.Select(x => x._source);
                                }
                                else
                                {
                                    var _iFailHits = ((IEnumerable<dynamic>)failHitsResult.hits).Cast<dynamic>();
                                    list_hits = _iFailHits.Select(x => x._source);
                                }
                                // Ended
                                if ((hitsResult != null && hitsResult.hits.Count > 0) || (failHitsResult != null && failHitsResult.hits.Count > 0))
                                    UpdateDataSummary(jobProcessInfo, jobTrackingUpdateInfo, processType, ref success, inittime, searchResult, indexType, includeFields, indexName, ref hitsResult, list_hits, jobProcessInfo.ClientId,
                                        appConfig, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);

                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"{indexName}:[Doc Summary] Initial_Validation - Exception occured while updating index - {indexName},  {ex.Message}");
                        }


                    }
                }
            }
            return success;
        }

        private void UpdateDataSummary(JobProcessInfo jobProcessInfo, JobTrackingUpdateInfo jobTrackingUpdateInfo, JobProcessType processType, ref bool success, DateTime inittime, dynamic searchResult, IndexType indexType, List<string> includeFields, string indexName, ref dynamic hitsResult, IEnumerable<dynamic> list_hits, string clienId,
            ApplicationConfig appConfig, JobTracking client, dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            _logger.LogInformation($"NER Entities job has started for {indexName} for {hitsResult.hits.Count} records!");
            try
            {
                hitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                var source = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(list_hits));
                _logger.LogInformation($"Ner Jobs for {indexName} Calling the Paython Api");
                Dictionary<string, string> nerBulkData = GetBulkDataSummary(source, includeFields, jobProcessInfo.FeatureConfigurationSetting?.baseApi, clienId);
                _logger.LogInformation($"Ner Jobs for {indexName} Completed the Paython Api");
                //source = null;
                hitsResult = null;
                if (nerBulkData != null)
                {
                    _logger.LogInformation($"Ner Jobs for {indexName}  Paython Api is returning data");
                    success = nerBulkData.Keys.Count > 0 ? true : false;
                    jobTrackingUpdateInfo.NoOfUpdatedRecords = nerBulkData.Keys.Count;
                    List<dynamic> elRecords = new List<dynamic>();
                    foreach (var record in nerBulkData)
                    {
                        Dictionary<string, object> doc = new Dictionary<string, object>();
                        doc[Constants.IS_DOC_SUMMARY_PROCESSED] = Constants.PROCESSED;
                        doc[Constants.DOC_SUMMARY] = record.Value;
                        elRecords.Add(new { update = new { _index = indexName, _id = record.Key } });
                        elRecords.Add(new { doc = doc, doc_as_upsert = false });
                        //var result = _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(elRecords));
                    }
                    var result = _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(elRecords));
                }
                else
                {
                    string incFields = Constants.IS_DOC_SUMMARY_PROCESSED;
                    Dictionary<string, object> doc = new Dictionary<string, object>();
                    List<dynamic> elRecords = new List<dynamic>();
                    var fields = incFields.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    List<Dictionary<string, object>> autoRequestData = source.Select(item => item.Where(x =>
                    fields.Any(f => f.ToLower() == x.Key.ToLower()) || x.Key.ToLower() == "id")
                    .ToDictionary(i => i.Key, i => i.Value)
                    ).ToList();

                    foreach (var item in autoRequestData)
                    {
                        string status = Convert.ToString(item.FirstOrDefault(x => x.Key == Constants.IS_DOC_SUMMARY_PROCESSED).Value);
                        if (status == Constants.INCOMPLETE)
                        {
                            doc[Constants.IS_DOC_SUMMARY_PROCESSED] = Constants.UNPROCESSED;
                        }
                        else
                        {
                            doc[Constants.IS_DOC_SUMMARY_PROCESSED] = Constants.INCOMPLETE;
                        }
                        var id = item.FirstOrDefault(x => x.Key == "id").Value;
                        elRecords.Add(new { update = new { _index = indexName, _id = id } });
                        elRecords.Add(new { doc = doc, doc_as_upsert = true });
                    }
                    _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(elRecords));
                    _logger.LogInformation($"NER Entities job has completed for {indexName} for {indexType.batchSize} records!");

                    jobTrackingUpdateInfo.Status = success;
                    string nerJobResponse = JsonConvert.SerializeObject(nerBulkData);

                    CommonUtility.UpdateJobByProcessType(client, dbJobTracking, processType, jobTrackingUpdateInfo, appConfig, jobHistoryFactory, JobTrackingFactory, nerJobResponse);

                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"{indexName}:[NER] Initial_Validation - Exception occured while processing cognitive response {Environment.NewLine} Exception : {ex.Message} {Environment.NewLine} {ex.StackTrace}");
            }
            var timeelapsed = inittime - DateTime.Now;
        }


        private Dictionary<string, NER_Entities> GetBulkNEREntities(List<Dictionary<string, object>> hits, List<string> includeFields, string baseApi, string clientId)
        {
            //NER_Entities ner = null;
            Dictionary<string, NER_Entities> nerData = null;
            try
            {
                if (includeFields.Count > 0)
                {
                    var fields = includeFields;
                    NerResult nerInput = new NerResult();

                    nerInput.data = hits.Select(item => item.Where(x =>
                    fields.Any(f => f.ToLower() == x.Key.ToLower()) || x.Key.ToLower() == "id")
                    .ToDictionary(i => i.Key, i => i.Value)
                    ).ToList();
                    nerInput.client_id = Convert.ToInt32(clientId);
                    string result = string.Empty;
                    //string url = "cognitive/ner/return_entities";
                    string autoSuggestionResult = string.Empty;
                    using (var iNERProcess = new NERProcess())
                    {
                        result = iNERProcess.NERDataProcess(nerInput, baseApi, Constants.NER_URL);
                    }
                    dynamic resultItem = "";
                    if (result != null)
                    {
                        resultItem = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(result);
                    }
                    Dictionary<string, dynamic> obj = new Dictionary<string, dynamic>();
                    if (resultItem != null && resultItem.ContainsKey("entities"))
                    {
                        Dictionary<string, dynamic> resultValue = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(JsonConvert.SerializeObject(resultItem["entities"]));
                        if (resultValue != null)
                        {
                            nerData = resultValue.Select(x => new KeyValuePair<string, NER_Entities>(x.Key, new NER_Entities
                            {
                                Location = x.Value["Location"].ToObject<string[]>(),
                                Organization = x.Value["Organization"].ToObject<string[]>(),
                                Cardinal = x.Value["Cardinal"].ToObject<string[]>(),
                                Date = x.Value["Date"].ToObject<string[]>(),
                                Percentage = x.Value["Percentage"].ToObject<string[]>(),
                                Person = x.Value["Person"].ToObject<string[]>(),
                            })).ToDictionary(i => i.Key, i => i.Value);
                        }
                        else
                        {
                            _logger.LogInformation("Ner Entities Api is returuning null data");
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Initial_Validation - Exception raised while cognitive process, exception message:  {ex.Message}  {ex.StackTrace}");
            }
            return nerData;
        }



        #region privateMethods
        private bool BuildDataDictionaryIndex(dynamic data, string indexName)
        {
            var isSuccess = false;
            if (!string.IsNullOrEmpty(data))
            {
                _logger.LogInformation($"Data Dictionary DeserializeObject for {indexName} ");

                var results = JsonConvert.DeserializeObject<dynamic>(data);
                var listElasticItems = new List<dynamic>();
                if (results.dictionary_index != null)
                {
                    List<Dictionary<string, object>> res_dic = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(results.dictionary_index));
                    if (res_dic.Any())
                    {
                        _logger.LogInformation($"Data Dictionary for {indexName} is building");
                        for (int i = 0; i < res_dic.Count; i++)
                        {
                            //var result = results.dictionary_index[i];
                            //result.ClientId = clientId;
                            listElasticItems.Add(new { update = new { _index = indexName, _id = i + 1 } });
                            listElasticItems.Add(new { doc = JsonConvert.DeserializeObject<DataDictionary_Elastic>(JsonConvert.SerializeObject(res_dic[i])), doc_as_upsert = true });
                        }
                        _iElasticIndexBuilder.BuildIndex<DataDictionary_Elastic>(indexName, true, res_dic[0]);

                        var setting = JsonConvert.SerializeObject(new { index = new { max_terms_count = 9999999 } });


                        _iElasticConnector._lowLevelClient.Indices.UpdateSettings<StringResponse>(indexName, PostData.String(setting));
                        _logger.LogInformation($"Data Dictionary for {indexName} is Completed");
                    }
                    else
                    {
                        _logger.LogInformation($"No record found in data dictionary for {indexName} ");
                    }

                }
                else
                {
                    _logger.LogInformation($"Data Dictionary result.dictionary_index is  null for {indexName} ");

                }
                results = null;
                StringResponse resp = null;
                int bulkSize = 10000;
                if (listElasticItems.Count > 0)
                {
                    for (int i = 0; i < (int)Math.Ceiling(listElasticItems.Count * 1.0 / bulkSize * 1.0); i++)
                    {
                        var batch = listElasticItems.Skip((int)i * bulkSize).Take(bulkSize);
                        resp = _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(batch), new BulkRequestParameters() { });
                    }

                    var indexExistsResult = _iElasticConnector._elasticClient.Indices.Exists(new IndexExistsRequest(indexName));
                    if (resp != null && resp.Success && indexExistsResult.Exists)
                    {
                        isSuccess = true;
                        _logger.LogInformation($"{indexName}:[DataDictionary] data created successfully.");
                    }
                }
                else
                {
                    _logger.LogError($"{indexName}:[DataDictionary] has not been created successfully.");
                }
                listElasticItems = null;
            }
            else
            {
                _logger.LogInformation($"Data Dictionary response data is null for {indexName}");
            }
            return isSuccess;
        }

        private Dictionary<string, string> GetBulkDataSummary(List<Dictionary<string, object>> hits, List<string> includeFields, string baseApi, string clientId)
        {
            //NER_Entities ner = null;
            Dictionary<string, string> nerData = null;
            try
            {
                //if (includeFields.Count > 0)
                {
                    var fields = includeFields;
                    NerResult nerInput = new NerResult();

                    string pageKey = "DocumentPage";
                    dynamic d = new
                    {
                        data = hits.Select(x =>
                        {
                            string pageKey = "DocumentPage";
                            List<string> pageList = new List<string>();
                            if (x != null && x.ContainsKey(pageKey))
                            {
                                if (x[pageKey] != null)
                                {
                                    var list = ((N.JArray)x[pageKey]);
                                    int take = (list.Count * 10) / 100;
                                    take = take < 10 ? 10 : take;
                                    pageList = list.Take(take).Select(x =>
                                    {
                                        if (x["text"] != null)
                                        {
                                            return x["text"].ToString();
                                        }
                                        return "";
                                    }).ToList();

                                }
                            }
                            return new { id = x.ContainsKey("id") ? x["id"] : "", pages = pageList };
                        }).ToList(),
                        client_id = clientId,
                        project_id = "0",
                        sentence_count = 5,
                        number_of_words_per_sentences = 10
                    };

                    nerInput.client_id = Convert.ToInt32(clientId);
                    string result = string.Empty;
                    //string url = "cognitive/ner/return_entities";
                    string autoSuggestionResult = string.Empty;
                    using (var iNERProcess = new NERProcess())
                    {
                        result = iNERProcess.NERDataProcess(d, baseApi, Constants.DATA_SUMMARY);
                    }
                    dynamic resultItem = "";
                    if (result != null)
                    {
                        resultItem = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(result);
                    }
                    Dictionary<string, dynamic> obj = new Dictionary<string, dynamic>();
                    if (resultItem != null && resultItem.ContainsKey("result") && resultItem["result"] != null && resultItem["result"].ContainsKey("data"))
                    {
                        var resultValue = (N.JArray)resultItem["result"]["data"];
                        if (resultValue != null)
                        {
                            nerData = resultValue.Select(x => new KeyValuePair<string, string>(x["id"]?.ToString(), x["summary"]?.ToString())).ToDictionary(i => i.Key, i => i.Value);
                        }
                        else
                        {
                            _logger.LogInformation("Ner Entities Api is returuning null data");
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Initial_Validation - Exception raised while cognitive process, exception message:  {ex.Message}  {ex.StackTrace}");
            }
            return nerData;
        }

        private Dictionary<string, AutoSuggestion_Entities> GetBulkAutoSuggestionEntities(List<Dictionary<string, object>> hits, IndexType indexType, string baseApi, JobDeatil jobDeatil)
        {
            Dictionary<string, AutoSuggestion_Entities> autoSuggestionData = new Dictionary<string, AutoSuggestion_Entities>();
            try
            {
                Dictionary<string, Dictionary<string, dynamic>> finalResultValue = new Dictionary<string, Dictionary<string, dynamic>>();

                var includeFields = indexType.includeFields.Count > 0 ? indexType.includeFields : null;

                if (includeFields.Count > 0)
                {
                    var fields = includeFields;
                    List<Dictionary<string, object>> autoRequestData = hits.Select(item => item.Where(x =>
                    fields.Any(f => f.ToLower() == x.Key.ToLower()) || x.Key.ToLower() == "id" || x.Key.ToLower() == Constants.PROJECT_ID.ToLower())
                    .ToDictionary(i => i.Key, i => i.Value)
                    ).ToList();
                    string result = string.Empty;

                    foreach (Dictionary<string, object> item in autoRequestData)
                    {
                        //item["projectId"] = projectId;
                        if (indexType.isDocument && item.ContainsKey(Constants.CONTENT))
                        {
                            string data = item[Constants.CONTENT].ToString();
                            item[Constants.CONTENT] = CommonUtility.CleanData(data);
                        }
                    }

                    AutoResultSuggestion autoResultSuggestion = new AutoResultSuggestion();
                    autoResultSuggestion.SuggestionGrm_Limit = indexType.suggestionGramLimit;
                    autoResultSuggestion.ExcludeAlphaNumericSuggestion = jobDeatil.ExcludeAlphaNumericSuggestion;
                    autoResultSuggestion.AutoSuggestionInputData = autoRequestData;
                    string autoSuggestionResult = string.Empty;
                    using (var iNERProcess = new NERProcess())
                    {

                        autoSuggestionResult = iNERProcess.NERDataProcess(autoResultSuggestion, baseApi, Constants.AUTO_SUGGESTIONS_URL);
                    }
                    if (autoSuggestionResult != null && autoSuggestionResult != "")
                    {
                        var autoSuggestionResultItem = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(autoSuggestionResult);
                        var suggestions = JsonConvert.SerializeObject(autoSuggestionResultItem[Constants.SUGGESTIONS]);

                        if (suggestions != "null")
                        {
                            Dictionary<string, dynamic> ob1j = new Dictionary<string, dynamic>();
                            dynamic suggestionValue = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(suggestions);
                            foreach (dynamic suggestionPair in suggestionValue)
                            {
                                AutoSuggestion_Entities autoSuggestion = new AutoSuggestion_Entities();
                                Dictionary<string, dynamic> resultValue = new Dictionary<string, dynamic>();
                                dynamic value = suggestionPair.Value;
                                dynamic key = suggestionPair.Key;
                                AutoSuggestion_Entities suggestion = new AutoSuggestion_Entities();

                                foreach (dynamic item in value)
                                {
                                    dynamic data = item.Value;
                                    var suggest = Constants.SUGGESTIONS + "_" + (Convert.ToInt32(item.Name) + 1);
                                    resultValue.Add(suggest, data);
                                }
                                autoSuggestion.SuggestionData = resultValue.Values;
                                autoSuggestionData.Add(key, autoSuggestion);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Auto-Suggestions Api is returuning null data");
                        }
                    }
                }
                //}
            }
            catch (Exception ex)
            {
                _logger.LogError($"Initial_Validation - Exception raised while cognitive process, exception message:  {ex.Message}  {ex.StackTrace}");
            }
            return autoSuggestionData;
        }

        public bool FetchAndUpdateNEREntities(JobDeatil jobDetail, JobProcessInfo jobProcessInfo, ApplicationConfig applicationConfig, IUtilityFunctions utilityFunctions, JobTrackingUpdateInfo jobTrackingUpdateInfo, IMongoConfigFactory nerEntityRulerData, JobProcessType jobProcessType)
        {
            throw new NotImplementedException();
        }

        public bool FetchAndUpdateNEREntities(JobDeatil jobDetail, JobProcessInfo jobProcessInfo, ApplicationConfig applicationConfig, IUtilityFunctions utilityFunctions, JobTrackingUpdateInfo jobTrackingUpdateInfo, IMongoConfigFactory nerEntityRulerData, JobProcessType jobProcessType, JobTracking client)
        {
            throw new NotImplementedException();
        }

        #endregion

    }
}





