using SearchLego.DataSeeder.Entities;

namespace SearchLego.DataSeeder.Common
{
    public interface IUtilityFunctions
    {
        string GetIndexName(string indexType, string clientId, string indexPrefix);
        void BuildCrawlHistory(CrawlSetting crawlSetting, bool IsFullCrawl, string lastUpdateDate, int recordsUpdated);
        string GetIndexName(JobProcessInfo jobProcessInfo, ApplicationConfig appConfig, IndexType indexType);
        string GetIndexPrefix(JobDeatil job, Tenant currentTenant);
        string GetConnectionString(ApplicationConfig configDetail, Tenant tenant);
    }
}
