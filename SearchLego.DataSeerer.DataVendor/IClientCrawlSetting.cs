using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using System.Collections.Generic;

namespace SearchLego.DataSeerer.Integration
{
    public interface IClientCrawlSetting
    {

        void BuildClientWiseSetting(List<string> lstclient, JobDeatil objJobDetail, IMongoConfigFactory iMongoClientCrawlFactory, IUtilityFunctions iUtilityFunctions, IList<Tenant> tenantList);
        CrawlSetting GetSettingById(IMongoConfigFactory mongoClientCrawlFactory, string id);
        void UpdateSetting(IMongoConfigFactory mongoClientCrawlFactory, CrawlSetting objCrawlSetting);
        void AddSetting(IMongoConfigFactory mongoClientCrawlFactory, CrawlSetting objCrawlSetting);

    }
}
