using System.Collections.Generic;

namespace SearchLego.DataSeeder.Elastic
{
    public class Suggestion
    {
        public List<string> Input { get; set; }

        
    }
    public class SingleSuggestion
    {
        public string Input { get; set; }
    }
    public class AutoResultSuggestion
    {
        public int SuggestionGrm_Limit { get; set; }

        public List<Dictionary<string, object>> AutoSuggestionInputData;

        public bool ExcludeAlphaNumericSuggestion { get; set; }
    }
    public class NerResult
    {
        public int client_id { get; set; }

        public List<Dictionary<string, object>> data;

    }
}
