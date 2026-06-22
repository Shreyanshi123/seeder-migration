using SearchLego.DataSeeder.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SearchLego.DataSeeder.Common
{
    public class UtilityFunctions : IUtilityFunctions
    {
        public string GetIndexName(string indexType, string clientId, string indexPrefix)
        {
            return string.IsNullOrEmpty(indexPrefix) ? $"{indexType}_{clientId}" : $"{indexPrefix}_{indexType}_{clientId}";
        }
        public string GetIndexPrefix(JobDeatil job, Tenant currentTenant)
        {
            if (currentTenant != null && currentTenant.TenantType.ToString().Equals(TenantConnectionType.Multi.ToString()))
                return !string.IsNullOrEmpty(currentTenant.Prefix) ? currentTenant.Prefix : "";
            else
                return !string.IsNullOrEmpty(job.IndexPrefix) ? job.IndexPrefix : "";
        }

        public void BuildCrawlHistory(CrawlSetting crawlSetting, bool IsFullCrawl, string lastUpdateDate, int recordsUpdated)
        {
            List<CrawlHistory> lstCrawlHistories = (crawlSetting.CrawlHistory == null) ? new List<CrawlHistory>() : crawlSetting.CrawlHistory;
            lstCrawlHistories.Add(new CrawlHistory() { CrawlType = IsFullCrawl ? CrawlType.Full : CrawlType.Incremental, ExecutionTime = lastUpdateDate, RecordsUpdated = recordsUpdated });
            crawlSetting.CrawlHistory = lstCrawlHistories.OrderByDescending(i => Convert.ToDateTime(i.ExecutionTime)).Take(100).ToList();
        }

        public string GetIndexName(JobProcessInfo jobProcessInfo, ApplicationConfig appConfig, IndexType indexType)
        {
            string indexName = null;
            if (jobProcessInfo.IndexTypes.Any(x => x == indexType.type))
            {
                var jobIndexPrefix = appConfig.JobDeatil.FirstOrDefault(x => x.IndexType == indexType.type);
                string prefix = (jobIndexPrefix != null ? jobIndexPrefix.IndexPrefix : "") ?? "";

                var jobdetails = new JobDeatil()
                {
                    IndexPrefix = prefix
                };
                var currentTenant = ApplicationConfig.TenantCollections?.Where(t => t.TenantId == jobProcessInfo.ClientId)?.FirstOrDefault();

                indexName = this.GetIndexName(indexType.type, jobProcessInfo.ClientId, GetIndexPrefix(jobdetails, currentTenant));
            }

            return indexName;
        }
        public string GetConnectionString(ApplicationConfig configDetail, Tenant tenant)
        {
            string connectionString = String.Empty;
            if (configDetail != null)
            {
                connectionString = configDetail.IsAzureVaultEnabled ? tenant?.AzureVaultConnectionKey : tenant?.ConnectionString;
            }

            return connectionString;
        }
    }
}
