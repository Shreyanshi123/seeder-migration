
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using System.Collections.Generic;
using System.Linq;

namespace SearchLego.DataSeerer.Integration
{
    public class ClientCrawlSetting : IClientCrawlSetting
    {
        public void BuildClientWiseSetting(List<string> lstclient, JobDeatil objJobDetail, IMongoConfigFactory iMongoClientCrawlFactory, IUtilityFunctions iUtilityFunctions, IList<Tenant> tenantList)
        {
            foreach (string clientId in lstclient)
            {
                Tenant tenant = tenantList?.Any() ?? false ? tenantList.Where(t => t.TenantId == clientId).FirstOrDefault() : null;

                string clientsettingId = iUtilityFunctions.GetIndexName(objJobDetail.IndexType, clientId, iUtilityFunctions.GetIndexPrefix(objJobDetail, tenant));
                var clientSetting = iMongoClientCrawlFactory.GetById(clientsettingId);
                if (clientSetting == null)
                {
                    CrawlSetting objcrawlSetting = new CrawlSetting();
                    objcrawlSetting._id = clientsettingId;
                    objcrawlSetting.IndexName = clientsettingId;
                    objcrawlSetting.IsForcedToFullCrawl = false;
                    objcrawlSetting.NoUpdateInConfig = true;
                    objcrawlSetting.IndexType = objJobDetail.IndexType;
                    objcrawlSetting.ClientId = clientId;
                    objcrawlSetting.DisableAutomatedFullCrawl = false;
                    objcrawlSetting.IsAutoSuggestionExecutionRequired = true;
                    string crawlerSettingJosn = JsonConvert.SerializeObject(objcrawlSetting);
                    iMongoClientCrawlFactory.Add(BsonSerializer.Deserialize<BsonDocument>(crawlerSettingJosn));
                }
            }
        }

        public CrawlSetting GetSettingById(IMongoConfigFactory mongoClientCrawlFactory, string id)
        {
            var item = mongoClientCrawlFactory.GetById(id);
            if (item != null)
                return BsonSerializer.Deserialize<CrawlSetting>(item);
            else
                return null;
        }

        public void UpdateSetting(IMongoConfigFactory mongoClientCrawlFactory, CrawlSetting objCrawlSetting)
        {
            var objClientSetting = BsonSerializer.Deserialize<BsonDocument>(JsonConvert.SerializeObject(objCrawlSetting));
            if (objClientSetting != null)
                mongoClientCrawlFactory.Update(objClientSetting);

        }

        public void AddSetting(IMongoConfigFactory mongoClientCrawlFactory, CrawlSetting objCrawlSetting)
        {
            var objClientSetting = BsonSerializer.Deserialize<BsonDocument>(JsonConvert.SerializeObject(objCrawlSetting));
            if (objClientSetting != null)
                mongoClientCrawlFactory.Add(objClientSetting);
        }
    }
}
