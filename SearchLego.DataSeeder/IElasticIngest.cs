using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using System.Collections.Generic;
using System.Data;

namespace SearchLego.DataSeeder.Elastic
{
    public interface IElasticIngest
    {
        bool BulkUpdateWithAttachment(string jsonData, JobDeatil jobDetail, string indexName, ApplicationConfig jobsDetail,
            IEnumerable<DataRow> configFieldsByClient, ClientConfiguration clientConfig,IMongoConfigFactory mongoConfigFactory, out int recordsUpdated);
        bool BulkUpdateDocument(string jsonData, JobDeatil jobDetail, string indexName, IEnumerable<DataRow> configFieldsByClient,
             ClientConfiguration clientConfig, out int recordsUpdated);
        bool GeneratePDFUpdateDocContentPageWise(JobDeatil jobDetail, ApplicationConfig jobsDetail, string indexName, bool isForcedExecution, IMongoConfigFactory mongoConfigFactory);
        Dictionary<string, object> GetFirstObjectForMapping(string jsonData);
        Dictionary<string, object> GetProjectIdFromJson(string jsonData);
        SingleSuggestion[] CreateSuggestions(IDictionary<string, object> items, JobDeatil jobDetail);

    }
}
