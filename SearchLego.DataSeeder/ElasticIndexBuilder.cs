using Elasticsearch.Net;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SearchLego.DataSeeder.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace SearchLego.DataSeeder.Elastic
{
    public class ElasticIndexBuilder : IElasticIndexBuilder
    {
        private readonly IElasticConnector _elasticConnector;
        public string IndexName { get; set; }
        public ElasticIndexBuilder(IElasticConnector elasticConnector)
        {
            _elasticConnector = elasticConnector;
        }

        public void BuildIndex<T>(string indexName, bool isReindex, Dictionary<string, object> mappingObject, int suggestionsGramLimit = 5) where T : class
        {


            // _elasticConnector._lowLevelClient.Indices.PutMapping();


            IndexName = indexName;
            var indexExistsResult = _elasticConnector._elasticClient.Indices.Exists(new IndexExistsRequest(IndexName));

            if (!indexExistsResult.Exists)
            {
                var response = _elasticConnector._elasticClient.Indices.Create(IndexName, f => f.Mappings(d => d.Map<T>(r => r.AutoMap())));
                if (response.IsValid)
                {
                    UpdateIndexSetting(IndexName);
                    UpdateIndexMapping(indexName, mappingObject, suggestionsGramLimit);
                }
                else
                    throw new Exception($"Unable to create index '{IndexName}', exception: {response.DebugInformation}");
            }
            else
            {
                if (isReindex)
                {
                    _elasticConnector._elasticClient.Indices.Delete(new DeleteIndexRequest(IndexName));

                    var response = _elasticConnector._elasticClient.Indices.Create(IndexName, f => f.Mappings(d => d.Map<T>(r => r.AutoMap())));
                    if (response.IsValid)
                    {
                        UpdateIndexSetting(IndexName);
                        UpdateIndexMapping(indexName, mappingObject, suggestionsGramLimit);
                    }
                    else
                        throw new Exception($"Unable to create index '{IndexName}', exception: {response.DebugInformation}");
                }

            }
        }

        public void BuildIndexByClient<T>(string indexType, string clientId, bool isReindex, bool EnabledSynonyms, Dictionary<string, object> mappingObject) where T : class
        {
            IndexName = $"{indexType}_{clientId}";
            var indexExistsResult = _elasticConnector._elasticClient.Indices.Exists(new IndexExistsRequest(IndexName));

            if (!indexExistsResult.Exists)
            {
                _elasticConnector._elasticClient.Indices.Create(IndexName, f => f.Mappings(d => d.Map<T>(r => r.AutoMap())));
                if (EnabledSynonyms)
                {
                    UpdateIndexSetting(IndexName);
                    UpdateIndexMapping(IndexName, mappingObject);

                }
            }
            else
            {
                if (isReindex)
                {
                    _elasticConnector._elasticClient.Indices.Delete(new DeleteIndexRequest(IndexName));
                    _elasticConnector._elasticClient.Indices.Create(IndexName, f => f.Mappings(d => d.Map<T>(r => r.AutoMap())));
                    if (EnabledSynonyms)
                    {
                        UpdateIndexSetting(IndexName);
                        UpdateIndexMapping(IndexName, mappingObject);
                    }

                }

            }

            // _elasticConnector._lowLevelClient.Indices.PutMapping();



        }



        /// <summary>
        /// get field mapping based on field type
        /// </summary>
        /// <param name="fieldtype"></param>
        /// <returns></returns>
        private dynamic GetFieldMappingForType(object fieldtype)
        {
            dynamic mappingForField = null;

            string type = (fieldtype != null) ? (fieldtype.ToString() == "content_attachment" ? "content_attachment" : fieldtype.GetType().Name) : "string";
            type = fieldtype?.ToString() == "auto_suggestion" ? "auto_suggestion" : type;
            string fieldwiseSettingJSON = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), @"Config\FieldsMappingSetting.json"));
            if (!string.IsNullOrEmpty(fieldwiseSettingJSON))
            {
                dynamic fieldwiseSetting = JObject.Parse(fieldwiseSettingJSON);
                switch (type.ToLower())
                {
                    case "string":
                        mappingForField = fieldwiseSetting.string_field;
                        break;
                    case "date":
                    case "datetime":
                        mappingForField = fieldwiseSetting.date_field;
                        break;
                    case "int64":
                    case "int32":
                    case "long":
                    case "integer":
                        mappingForField = fieldwiseSetting.long_field;
                        break;
                    case "boolean":
                        mappingForField = fieldwiseSetting.boolean_field;
                        break;
                    case "content_attachment":
                        mappingForField = fieldwiseSetting.content_attachment;
                        break;
                    case "auto_suggestion":
                        mappingForField = fieldwiseSetting.auto_suggestion;
                        break;
                    case "doc_summary":
                        mappingForField = fieldwiseSetting.doc_summary;
                        break;
                    default:
                        mappingForField = fieldwiseSetting.string_field;
                        break;

                }
            }
            else
            {
                throw new FileNotFoundException("FieldsMappingSetting.json file is not found");
            }
            return mappingForField;

        }

        //private dynamic MappingForCustomTypes(object type, string key)
        //{

        //}

        private void UpdateIndexSetting(string indexName)
        {
            string setting = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), @"Config\IndexSetting.json"));
            _elasticConnector._elasticClient.Indices.Close(indexName);
            var response = _elasticConnector._lowLevelClient.Indices.UpdateSettings<StringResponse>(indexName, PostData.String(setting));
            _elasticConnector._elasticClient.Indices.Open(indexName);
            if (!response.Success)
                throw new Exception($"Unable to update setting in index '{indexName}',exception:{response.DebugInformation}");

        }

        /// <summary>
        /// creating custom mappings for index
        /// </summary>
        /// <param name="indexName"></param>
        /// <param name="mappingObject"></param>
        private void UpdateIndexMapping(string indexName, Dictionary<string, object> mappingObject, int suggestionsGramLimit = 5)
        {
            //geting existing mapping
            var response = _elasticConnector._lowLevelClient.Indices.GetMapping<StringResponse>(indexName);
            if (!response.Success)
                throw new Exception($"Unable to get existing mapping to update mapping in index '{indexName}',exception:{response.DebugInformation}");

            dynamic existingmapping = null;
            dynamic existingMappingResponse = JObject.Parse(response.Body);
            if (existingMappingResponse != null)
                existingmapping = existingMappingResponse[indexName].mappings.properties;

            Dictionary<string, dynamic> listofFieldAndMapping = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(JsonConvert.SerializeObject(existingmapping));
            //now, creating mapping based the 1st row of the data table
            if (listofFieldAndMapping == null)
                listofFieldAndMapping = new Dictionary<string, dynamic>();

            if (!indexName.EndsWith(Constants.AUTO_COMPLETE))
            {
                foreach (var key in mappingObject.Keys)
                {
                    if (listofFieldAndMapping.ContainsKey(key))
                        continue;
                    object type;
                    mappingObject.TryGetValue(key, out type);
                    dynamic mappingJson = GetFieldMappingForType(type);
                    listofFieldAndMapping.Add(key, mappingJson);
                }
            }
            else
            {
                listofFieldAndMapping = new Dictionary<string, dynamic>();
                foreach (var key in mappingObject.Keys)
                {
                    for (int i = 1; i <= suggestionsGramLimit; i++)
                    {
                        dynamic mappingJson = GetFieldMappingForType("auto_suggestion");
                        listofFieldAndMapping.Add("suggestions" + Constants.UNDERSCORE + i + Constants.UNDERSCORE + key, mappingJson);
                    }
                }
            }

            if (listofFieldAndMapping.ContainsKey("content"))
            {
                dynamic mappingJson = GetFieldMappingForType("content_attachment");
                listofFieldAndMapping["Content"] = mappingJson;

            }
            
            dynamic mapping_doc_summary = GetFieldMappingForType("doc_summary");

            if (listofFieldAndMapping.ContainsKey(Constants.DOC_SUMMARY))
            {
                listofFieldAndMapping[Constants.DOC_SUMMARY] = mapping_doc_summary;
            }
            else
            {
                listofFieldAndMapping.Add(Constants.DOC_SUMMARY, mapping_doc_summary);
            }

            var listofJson = JsonConvert.SerializeObject(listofFieldAndMapping);
            dynamic objFieldMapping = new
            {
                properties = listofFieldAndMapping
            };
            //now, updating mapping in ES
            string jsonForMapping = JsonConvert.SerializeObject(objFieldMapping);
            _elasticConnector._elasticClient.Indices.Close(IndexName);
            var updateMappingResponse = _elasticConnector._lowLevelClient.Indices.PutMapping<StringResponse>(indexName, PostData.String(jsonForMapping));
            _elasticConnector._elasticClient.Indices.Open(IndexName);
            if (!updateMappingResponse.Success)
                throw new Exception($"Unable to update mapping in index '{indexName}',exception:{updateMappingResponse.DebugInformation}");

        }
    }


}
