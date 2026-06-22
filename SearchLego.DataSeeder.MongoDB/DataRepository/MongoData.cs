using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using N = Newtonsoft.Json.Linq;

namespace SearchLego.DataSeeder.MongoDB
{
    public class MongoData
    {
        private IMongoDatabase _db = null;
        public MongoData(IMongoDatabase db)
        {
            _db = db;
        }

        public string FetchData(DateTime time, string clientId, string collectionName)
        {
            var query = new BsonDocument
            {
                {"tenantId", clientId },
                {"lastUpdated" , new BsonDocument {
                    { "$gt" , time}
                }}
            };


            var data = _db.GetCollection<BsonDocument>(collectionName).Find(query).Project(o => prepareDocument(o)).ToList().Where(x => x != null).ToList();
            var convertedData = data.ConvertAll(BsonTypeMapper.MapToDotNetValue);
            return convertedData.Count == 0 ? "" : Newtonsoft.Json.JsonConvert.SerializeObject(convertedData);
        }

        private BsonDocument prepareDocument(BsonDocument o)
        {
            try
            {
                string filePath = o["_id"] + "." + getValue(o, "fileExtension");
                string useTenantSourceFile = getValue(o, "useTenantSourceFile");
                Boolean parsedValue;
                if (Boolean.TryParse(useTenantSourceFile, out parsedValue))
                {
                    if (parsedValue)
                    {
                        filePath = getValue(o, "originalFileName");
                    }
                }

                var doc = new BsonDocument
            {

                {"id", o["_id"] },
                {"clientId",  getValue(o, "tenantId") },
                {"projectId",  "0" },
                {"Title", getValue(o, "fileName")+ "." + getValue(o, "fileExtension") },
                {"IsActive", o["isActive"] },
                {"FilePath", filePath},
                {"CreatedDate", getValue(o, "createdOn") },
                {"CreatedBy", getValue(o,"createdBy") },
                {"FORMAT", getValue(o,"fileFormat") },
                {"SourceType", getValue(o,"sourceType") },
                {"SourceName", getValue(o,"sourceName") },
                {"UpdatedDate", getValue(o, "lastUpdated") },
                {"FileName", getValue(o, "fileName")  },
                {"SourceId", getValue(o, "sourceId")  },
                {"Size", Convert.ToInt32(getValue(o, "fileSize"))  },
            };
                doc["AllTags"] = o.Contains("metadata") ? getTagsAndSetFlatFields(doc, o["metadata"]) : "";
                doc["Tag"] = doc["AllTags"];

                return doc;
            }
            catch (Exception ex)
            {

            }
            return null;
        }

        private string getValue(BsonDocument doc, string field)
        {
            return doc.Contains(field) ? doc[field].ToString() : string.Empty;
        }

        private string getTagsAndSetFlatFields(BsonDocument document, BsonValue doc)
        {
            string tag = "";
            if (doc != null && doc.GetType().Name != "BsonNull")
            {
                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };
                dynamic json = doc.ToJson(jsonWriterSettings);
                dynamic ditem = Newtonsoft.Json.Linq.JObject.Parse(json);
                tag = val(document, ditem);
            }

            return tag;
        }

        private string val(BsonDocument document, N.JObject obj)
        {
            string v = "";

            Func<N.JToken, string> getString = delegate (N.JToken y)
             {
                 return y.ToString();
             };


            Dictionary<string, object> dic = obj.ToObject<Dictionary<string, object>>();
            foreach (var item in dic)
            {
                if (item.Key != "key")
                {
                    if (item.Value != null && item.Value.GetType().Name == "JObject")
                    {
                        if (item.Value != null)
                        {
                            var bsonVal = ((N.JObject)item.Value)["key"];
                            if (bsonVal != null)
                            {
                                document[item.Key] = BsonValue.Create(bsonVal.ToString());

                                v += "^" + document[item.Key];

                            }
                        }

                    }
                    else if (item.Value.GetType().Name == "JArray")
                    {
                        List<string> s_list = new List<string>();
                        //var json = Newtonsoft.Json.JsonConvert.SerializeObject(item);
                        //if (json != null)
                        //{
                        //    var bsonValue = BsonDocument.Parse(json);
                        //    if (bsonValue != null && bsonValue.GetType().Name != "BsonNull" && bsonValue["Value"] != null)
                        //        document[item.Key] = bsonValue["Value"];
                        //}
                        foreach (var item2 in (N.JArray)item.Value)
                        {
                            if (item2 != null && item2.GetType().Name == "JObject")
                                v += val(document, (N.JObject)item2);
                            else if (item2 != null && item2.GetType().Name == "String" || item2.GetType().Name == "JValue")
                            {
                                v += "^" + item2.ToString();
                                s_list.Add(item2.ToString());
                            }
                        }
                        document[item.Key] = string.Join('^', s_list.ToArray());
                    }
                    else if (item.Value.GetType().Name == "String")
                    {
                        v += "^" + item.Value;
                    }
                }
            }
            return v;
        }

    }
}
