using Microsoft.Extensions.Logging;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using System.Collections.Generic;
using System.Data;

namespace SearchLego.DataSeerer.Integration
{
    public interface ISLBConfig
    {
        void ExecuteSlbConfig(string clientId, ILogger<dynamic> logger, IMongoConfigFactory iMongoConfigFactory, ISettingValue iSettingValue, DataSet objConfig, JobDeatil jobDeatil, string fileName, string format, IUtilityFunctions iUtilityFunctions);
        void UpdateSBLConfig(string clientId, ILogger<dynamic> logger, IMongoConfigFactory iMongoConfigFactory, IMongoConfigFactory mongoClientCrawlFactory, IClientCrawlSetting clientCrawlSetting, CrawlSetting crawlSetting, DataSet objConfig, JobDeatil jobDeatil, string format, IUtilityFunctions iUtilityFunctions, Tenant currentTenant);

        void UpdateSBLTabName(ILogger<dynamic> logger, IMongoConfigFactory iMongoConfigFactory, IEnumerable<DataRow> configFieldsByClient, string indexType);
    }
}