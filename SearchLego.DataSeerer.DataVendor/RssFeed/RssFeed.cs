using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Elastic;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Xml;

namespace SearchLego.DataSeerer.Integration.RssFeed
{
    public class RssFeed : IRssFeed
    {
        private readonly ILogger<dynamic> _logger;
        private ApplicationConfig applicationConfig;
        private IElasticIndexBuilder elasticIndexBuilder;
        private IElasticConnector elasticConnector;
        private readonly IUtilityFunctions _iUtilityFunctions;
        public RssFeed(ILogger<dynamic> logger, ApplicationConfig _applicationConfig, IElasticIndexBuilder _elasticIndexBuilder, 
            IElasticConnector _elasticConnector, IUtilityFunctions iUtilityFunctions)
        {
            _logger = logger;
            applicationConfig = _applicationConfig;
            elasticIndexBuilder = _elasticIndexBuilder;
            elasticConnector = _elasticConnector;
            _iUtilityFunctions = iUtilityFunctions;
        }

        public JobTrackingUpdateInfo GenerateRssFeed(string jobId, JobProcessInfo jobProcessInfo)
        {
            _logger.LogInformation($"Execution started for CognitiveProcessType ({JobProcessType.RssFeed.ToString()})");

            var jobTrackingUpdateInfo = new JobTrackingUpdateInfo() { SkipUpdate = true };
            var data = new List<dynamic>();
            foreach (var index in jobProcessInfo?.FeatureConfigurationSetting.indexTypes)
            {
                string url = index.type;
                XmlReader reader = XmlReader.Create(url);
                SyndicationFeed feed = SyndicationFeed.Load(reader);
                reader.Close();
                foreach (dynamic item in feed.Items)
                {
                    dynamic record = new ExpandoObject();
                    var dict = (IDictionary<string, object>)record;
                    foreach (var included in index.includeFields)
                    {
                        var itemValue = item.GetType().GetProperty(included).GetValue(item, null);

                        switch (itemValue?.GetType()?.ToString())
                        {
                            case "System.ServiceModel.Syndication.TextSyndicationContent":
                                dict[included] = itemValue?.Text;
                                break;
                            case "System.ServiceModel.Syndication.NullNotAllowedCollection`1[System.ServiceModel.Syndication.SyndicationLink]":
                                dict[included] = itemValue?[0].Uri.AbsoluteUri;
                                break;
                            default:
                                dict[included] = itemValue;
                                break;
                        }
                    }

                    string id = item.Id;
                    var newId = string.Join("", id.ToCharArray().Where(Char.IsDigit));
                    dict["id"] = string.IsNullOrEmpty(newId) ? Guid.NewGuid().ToString() : newId;
                    dict["SourceType"] = index.sourceType;
                    dict["SourceName"] = index.sourceName;
                    dict["LastUpdatedDate"] = DateTime.Now;
                    dict["projectId"] = "0";
                    dict["clientId"] = jobProcessInfo.ClientId;
                    dict[Constants.IS_NER_PROCESSED] = Constants.INITIAL_STATE;
                    dict[Constants.IS_AUTO_SUGGESTED] = Constants.INITIAL_STATE;
                    data.Add(dict);
                }
            }
            if (data.Count > 0)
            {
                jobTrackingUpdateInfo.SkipUpdate = false;

                var currentTenant = ApplicationConfig.TenantCollections?.Where(t => t.TenantId == jobProcessInfo.ClientId)?.FirstOrDefault();
                var jobdetails = new JobDeatil()
                {
                    IndexPrefix = ""
                };

                string indexName = _iUtilityFunctions.GetIndexName(jobProcessInfo?.FeatureConfigurationSetting?.featureType?.ToLower(),jobProcessInfo?.ClientId,_iUtilityFunctions.GetIndexPrefix(jobdetails,currentTenant));

                jobTrackingUpdateInfo.Status = InsertIntoElastic(data, indexName);
                _logger.LogInformation($"Elastic RssFeedIngest has been done successfully for job id : {jobId}  ClientId : {jobProcessInfo?.ClientId} CognitiveProcessType '{jobProcessInfo?.FeatureConfigurationSetting?.featureType}' and FeatureType '{JobProcessType.RssFeed.ToString()}'");
                jobTrackingUpdateInfo.Status = InsertIntoMongo(data, jobProcessInfo);
                _logger.LogInformation($"Mongo RssFeed has been done successfully for job id : {jobId}  ClientId : {jobProcessInfo?.ClientId} CognitiveProcessType '{jobProcessInfo?.FeatureConfigurationSetting?.featureType}' and FeatureType '{JobProcessType.RssFeed.ToString()}'");
            }
            return jobTrackingUpdateInfo;
        }

        private bool InsertIntoMongo(List<dynamic> data, JobProcessInfo jobProcessInfo)
        {
            IMongoDatabaseFactory mongoDatabaseFactory = new MongoDatabaseFactory();
            IMongoConfigFactory _modelConfigFactory = new MongoConfigUpdate(mongoDatabaseFactory.CreateDatabase(applicationConfig, jobProcessInfo?.ClientId, applicationConfig.SLBDocDb), MongoStaticName.RssFeed);

            foreach (var item in data)
            {
                var newItem = BsonSerializer.Deserialize<BsonDocument>(JsonConvert.SerializeObject(item, new JavaScriptDateTimeConverter()));
                newItem["_id"] = newItem["id"];
                if (newItem != null)
                    _modelConfigFactory.Update(newItem);
            }

            return true;
        }

        private bool InsertIntoElastic(List<dynamic> data, string indexName)
        {
            var success = false;
            try
            {
                List<Dictionary<string, object>> res_dic = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(data));
                if (res_dic.Any())
                {
                    var records = new List<dynamic>();

                    elasticIndexBuilder.BuildIndex<DataDictionary_Elastic>(indexName, false, res_dic[0]);

                    for (int i = 0; i < res_dic.Count; i++)
                    {
                        records.Add(new { update = new { _index = indexName, _id = res_dic[i]["id"] } });
                        records.Add(new { doc = res_dic[i], doc_as_upsert = true });
                    }
                    var resp = elasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(records), new BulkRequestParameters() { });
                    if (resp != null && resp.Success)
                    {
                        BuildAutoComplete(indexName);
                        success = true;
                    }
                    else
                    {
                        success = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error occured while inserting data to elastic RssFeed {DateTime.Now.ToString()}  - indexName {indexName}, InsertIntoElastic mehod call, {ex.Message}, {ex.StackTrace}");
                throw;
            }

            return success;
        }

        private void BuildAutoComplete(string indexName)
        {
            var autoCompleteIndexName = indexName + Constants.AUTO_COMPLETE;
            var indexExistsResult = elasticConnector._elasticClient.Indices.Exists(new IndexExistsRequest(autoCompleteIndexName));

            if (!indexExistsResult.Exists)
            {
                var listofFieldAndMapping = new Dictionary<string, dynamic>();
                string fieldwiseSettingJSON = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), @"Config\FieldsMappingSetting.json"));
                dynamic fieldwiseSetting = JObject.Parse(fieldwiseSettingJSON);
                for (int i = 1; i <= 10; i++)
                {
                    dynamic mappingJson = fieldwiseSetting.auto_suggestion;
                    listofFieldAndMapping.Add("suggestions" + Constants.UNDERSCORE + i + Constants.UNDERSCORE + "0", mappingJson);
                }
                dynamic objFieldMapping = new
                {
                    properties = listofFieldAndMapping
                };
                elasticIndexBuilder.BuildIndex<DataDictionary_Elastic>(autoCompleteIndexName, false, new Dictionary<string, object>());
                string jsonForMapping = JsonConvert.SerializeObject(objFieldMapping);
                var updateMappingResponse = elasticConnector._lowLevelClient.Indices.PutMapping<StringResponse>(autoCompleteIndexName, PostData.String(jsonForMapping));
                elasticConnector._elasticClient.Indices.Open(autoCompleteIndexName);
                if (!updateMappingResponse.Success)
                    throw new Exception($"Unable to update mapping in index '{autoCompleteIndexName}',exception:{updateMappingResponse.DebugInformation}");
            }
        }
    }
}
