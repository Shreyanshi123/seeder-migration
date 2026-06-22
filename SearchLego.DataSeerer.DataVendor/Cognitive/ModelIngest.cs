using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Elastic;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;
using SearchLego.DataSeeder.MongoDB.Host;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SearchLego.DataSeerer.Integration
{
    public class ModelIngest
    {
        private readonly ILogger<dynamic> _logger;
        private readonly IModelIngestService _modelIngestService;
        private readonly ApplicationConfig applicationConfig;
        private readonly IElasticConnector _iElasticConnector;

        public ModelIngest(ILogger<dynamic> logger,
                            ApplicationConfig appConfig,
                            IModelIngestService modelIngestService,
                            IElasticConnector iElasticConnector)
        {
            _logger = logger;
            _modelIngestService = modelIngestService;
            applicationConfig = appConfig;
            _iElasticConnector = iElasticConnector;
        }

        public JobTrackingUpdateInfo GenerateModelFiles(string jobId, ModelType modelType, JobProcessInfo jobProcessInfo, IUtilityFunctions utilityFunctions, ApplicationConfig appConfig,
            JobProcessType processType, JobTracking client, dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            var jobTrackingUpdateInfoResult = new JobTrackingUpdateInfo();

            if (CommonUtility.IsExecutionRequiredForClient(jobProcessInfo, Constants.MODEL_INGEST))
            {
                _logger.LogInformation($"Execution is required and {modelType} job has been started and client Id : {jobProcessInfo.ClientId} !");

                var modelConfig = new List<ModelConfig>();
                var result = new ModelConfig();
                try
                {
                    var projectIds = jobProcessInfo?.ClientAdditionalSetting?.projectIds;
                    if (projectIds != null)
                    {
                        foreach (var projectId in projectIds)
                        {
                            _logger.LogInformation($"{modelType} python api processing has been started for clientID : {jobProcessInfo.ClientId} and projectId : {projectId}!");
                            switch (modelType)
                            {
                                case ModelType.RelatedSearch:
                                    result = GeneratedRelatedSearchModel(jobProcessInfo, Convert.ToInt32(projectId), jobTrackingUpdateInfoResult, appConfig, JobProcessType.RelatedSearch, client, dbJobTracking, JobTrackingFactory, jobHistoryFactory);
                                    break;

                                case ModelType.PeopleAlsoSearch:
                                    result = GeneratedPeopleAlsoModel(Convert.ToInt32(projectId), jobProcessInfo, applicationConfig, utilityFunctions, jobTrackingUpdateInfoResult, client, dbJobTracking, JobProcessType.RelatedSearch, JobTrackingFactory, jobHistoryFactory);
                                    break;
                            }

                            _logger.LogInformation($"{modelType} python api processing has been completed for clientID : {jobProcessInfo.ClientId} and projectId : {projectId} and Response : {JsonConvert.SerializeObject(result)} ");

                            if (result != null && result.status)
                            {
                                _logger.LogInformation($"result is not empty for {modelType}");
                                result._id = Guid.NewGuid();
                                result.isLatest = true;

                                result.jobName = modelType.ToString();
                                result.clientId = Convert.ToInt32(jobProcessInfo.ClientId);
                                result.projectId = Convert.ToInt32(projectId);
                                modelConfig.Add(result);
                            }
                        }
                        if (modelConfig.Any())
                        {
                            InsertmodelLogs(modelConfig, jobId);
                        }
                    }
                    jobTrackingUpdateInfoResult.SkipUpdate = modelConfig.Any() ? false : true;
                    jobTrackingUpdateInfoResult.Status = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[model] Initial_Validation - Error occured while creating model file for job id: " + jobId + "Error Message :" + ex.Message);
                    jobTrackingUpdateInfoResult.Status = false;
                }

                _logger.LogInformation($"{modelType} job has been completed !");
            }
            else
            {
                jobTrackingUpdateInfoResult.SkipUpdate = true;
            }

            return jobTrackingUpdateInfoResult;
        }
        private ModelConfig GeneratedPeopleAlsoModel(int projectId, JobProcessInfo jobProcessInfo, ApplicationConfig appConfig, IUtilityFunctions utilityFunctions, JobTrackingUpdateInfo jobTrackingUpdateInfoResult, JobTracking client,
            dynamic dbJobTracking, JobProcessType processType, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            ModelConfig result = new ModelConfig();

            var indexTypes = jobProcessInfo?.FeatureConfigurationSetting?.indexTypes;
            var nerEntities = new Dictionary<string, List<string>>();
            int totalRecords = 0, totaltRespoCount = 0;
            List<Dictionary<string, List<string>>> abc = new List<Dictionary<string, List<string>>>();
            foreach (var indexType in indexTypes)
            {
                var includeFields = indexType.includeFields.Count > 0 ? indexType.includeFields : null;

                string indexName = utilityFunctions.GetIndexName(jobProcessInfo, appConfig, indexType);
                if (indexName != null)
                {
                    _logger.LogInformation($"People Also Search started for indexName : {indexName} and clientId : {jobProcessInfo.ClientId} and projectId : {projectId}");

                    var _totaltRespo = JsonConvert.DeserializeObject<dynamic>(_iElasticConnector._lowLevelClient.Count<StringResponse>(indexName, PostData.Serializable(new { })).Body);
                    if (_totaltRespo != null && _totaltRespo.count != null)
                    {
                        totalRecords = (int)_totaltRespo.count;
                        int processedRows = 0;
                        int batchSize = Convert.ToInt32(Constants.SIZE);
                        var loopExecutionTime = Math.Ceiling((decimal)totalRecords / (decimal)batchSize);
                        for (var i = 0; i < loopExecutionTime; i++)
                        {
                            CreateEntities(projectId, nerEntities, includeFields, indexName, processedRows, batchSize);
                            processedRows += batchSize;
                        }
                        totaltRespoCount += totalRecords;
                    }
                }
            }

            if (nerEntities.Count > 0)
            {
                _logger.LogInformation($"People Also Search started : NER count is {nerEntities.Count} for CognetivType : {jobProcessInfo.FeatureConfigurationSetting.featureType}");

                jobTrackingUpdateInfoResult.NoOfUpdatedRecords = totaltRespoCount;
                result = _modelIngestService.GenerateModelFile(new { client_id = jobProcessInfo.ClientId, project_id = projectId, entities = nerEntities }, jobProcessInfo.FeatureConfigurationSetting.baseApi, Constants.VECTOR_REPRESENATION_URL);

                _logger.LogInformation($"People Also Search started : python API is completed for indexName : {jobProcessInfo.FeatureConfigurationSetting.featureType}");

                jobTrackingUpdateInfoResult.Status = result.status;
                string _modelIngestServiceResponse = JsonConvert.SerializeObject(result);
                CommonUtility.UpdateJobByProcessType(client, dbJobTracking, processType, jobTrackingUpdateInfoResult, appConfig, jobHistoryFactory, JobTrackingFactory, _modelIngestServiceResponse);

            }

            return result;
        }

        private void CreateEntities(int projectId, Dictionary<string, List<string>> nerEntities, List<string> includeFields, string indexName, int processedRows, int batchSize)
        {
            var searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
            {
                from = processedRows,
                Size = batchSize,
                _source = Constants.NER_ENTITES,
                query = new
                {
                    match = new
                    {
                        projectId = new { query = projectId }
                    }
                }
            }));

            dynamic hitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;

            if (hitsResult != null && hitsResult.hits.Count > 0 && includeFields.Count > 0)
            {
                var _iHits = ((IEnumerable<dynamic>)hitsResult.hits).Cast<dynamic>();
                var list_hits = _iHits.Where(x => x._source["NEREntities"] != null)?.Select(x => x._source);
                if (list_hits != null && list_hits.Any())
                {
                    var source = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(list_hits));
                    var entitiesDictionary = JsonConvert.DeserializeObject<List<Dictionary<string, Newtonsoft.Json.Linq.JArray>>>(JsonConvert.SerializeObject(source?.SelectMany(x => x.Values)));
                    foreach (var entities in entitiesDictionary)
                    {
                        foreach (var field in includeFields)
                        {
                            var data = entities.ContainsKey(field) ? entities[field] : new Newtonsoft.Json.Linq.JArray();
                            GenerateList(data, field, nerEntities);
                        }
                    }
                }
            }
        }

        private ModelConfig GeneratedRelatedSearchModel(JobProcessInfo jobProcessInfo, int projectId, JobTrackingUpdateInfo jobTrackingUpdateInfo, ApplicationConfig appConfig, JobProcessType processType, JobTracking client,
            dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory)
        {
            _logger.LogInformation($"Related Search python api calling has been started for clientID : {jobProcessInfo.ClientId} and projectId : {projectId}!");

            ModelConfig result = new ModelConfig();
            result = _modelIngestService
        .GenerateModelFile(new { client_id = Convert.ToInt32(jobProcessInfo.ClientId), project_id = projectId }, jobProcessInfo.FeatureConfigurationSetting.baseApi, Constants.QUERY_EMBEDDINGS_URL);

            _logger.LogInformation($"Related Search python api calling has been completed for clientID : {jobProcessInfo.ClientId} and projectId : {projectId}!");

            string _modelIngestServiceResponse = JsonConvert.SerializeObject(result);

            jobTrackingUpdateInfo.Status = result.status;

            CommonUtility.UpdateJobByProcessType(client, dbJobTracking, processType, jobTrackingUpdateInfo, appConfig, jobHistoryFactory, JobTrackingFactory, _modelIngestServiceResponse);


            return result;
        }

        private void InsertmodelLogs(List<ModelConfig> modelConfig, string jobId)
        {
            try
            {
                _logger.LogInformation($" Insertion into modelConfig collection has been started for clientID : {modelConfig?.FirstOrDefault()?.clientId} ");

                IMongoConfigFactory _modelConfigFactory = new MongoClientSetting().GetDBObject(_logger, applicationConfig, MongoStaticName.ModelConfiguration);
                IMongoModelConfigRepository mongoModelConfigRepository = new MongoClientSetting().GetMongoConfigDBObject(_logger, applicationConfig, MongoStaticName.ModelConfiguration);

                foreach (var model in modelConfig)
                {
                    _logger.LogInformation($" Insertion into modelConfig collection has been started for clientID : {model.clientId} and projectId : {model.projectId} ");
                    //Find the Previous isLatest as false model config and remove it
                    bool islatest = false;
                    var item = mongoModelConfigRepository.GetModelConfig(model.clientId, model.projectId, model.jobName, islatest);

                    if (item != null)
                    {
                        item.TryGetValue("_id", out BsonValue bsonValue);
                        _modelConfigFactory.Remove(bsonValue.ToString());
                    }
                    //Find the Previous isLatest as true model config and update isLatest as false
                    islatest = true;
                    var prevRecord = mongoModelConfigRepository.GetModelConfig(model.clientId, model.projectId, model.jobName, islatest);
                    if (prevRecord != null)
                    {
                        prevRecord[Constants.IS_LATEST] = false;
                        _modelConfigFactory.Update(prevRecord);
                    }
                    // Added the new model config
                    var newModel = BsonSerializer.Deserialize<BsonDocument>(JsonConvert.SerializeObject(model));
                    if (newModel != null)
                        _modelConfigFactory.Add(newModel);

                    _logger.LogInformation($" Insertion into modelConfig collection has been completed for clientID : {model.clientId} and projectId : {model.projectId} ");

                }

                _logger.LogInformation($" Insertion into modelConfig collection has been fully completed for clientID : {modelConfig?.FirstOrDefault()?.clientId}");

            }
            catch (Exception ex)
            {
                _logger.LogError($"[model] Initial_Validation - Error occured while creating inserting model logs for job id: {jobId} and clientId : {modelConfig?.FirstOrDefault()?.clientId} . Error Message : {ex.Message}");
            }
        }

        private void GenerateList(Newtonsoft.Json.Linq.JArray entities, string field, Dictionary<string, List<string>> nerEntities)
        {
            var data = nerEntities.ContainsKey(field) ? nerEntities[field] : new List<string>();
            foreach (var entity in entities)
            {
                string value = entity.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    data.Add(value);
                }
            }

            nerEntities[field] = data;
        }

        private bool IsExecutionRequiredForClient(JobProcessInfo jobProcessInfo)
        {
            bool isRequired = false;

            if (!string.IsNullOrEmpty(jobProcessInfo.LastExecutedTime))
            {
                DateTime lastUpdated;
                var isValidDateTime = DateTime.TryParse(jobProcessInfo.LastExecutedTime, out lastUpdated);
                if (isValidDateTime)
                {
                    var totalDays = (DateTime.Now - lastUpdated).TotalDays;
                    if (totalDays > 1.0)
                    {
                        isRequired = true;
                    }
                }
            }
            else
            {
                isRequired = true;
            }

            return isRequired;
        }
    }
}

