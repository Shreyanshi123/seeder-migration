using System.Collections.Generic;

namespace SearchLego.DataSeeder.Elastic
{
    public interface IElasticIndexBuilder
    {
        public string IndexName { get; set; }
        void BuildIndexByClient<T>(string indexName, string clientId, bool isReindex, bool EnabledSynonyms, Dictionary<string, object> mappingObject) where T : class;
        void BuildIndex<T>(string IndexName, bool IsReindex, Dictionary<string, object> mappingObject, int suggestionsGramLimit = 5) where T : class;
    }
}
