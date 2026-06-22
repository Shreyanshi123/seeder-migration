using System;
using System.Collections.Generic;

namespace SearchLego.DataSeeder.Entities
{
    public class CrawlSetting
    {
        public string _id { get; set; }
        public string IndexType { get; set; }
        public string IndexName { get; set; }
        public string ClientId { get; set; }
        public string FullCrawlExecutionTime { get; set; } = Convert.ToDateTime("01 Jan 1900 23:59").ToString();
        public string LastUpdatedDate { get; set; }
        public string LastConfigUpdated { get; set; }
        public bool IsForcedToFullCrawl { get; set; }
        public List<CrawlHistory> CrawlHistory { get; set; }
        public bool IsScheduledToFullCrawl { get; set; }
        public DataDictionary DataDictionary { get; set; }
        public bool DisableAutomatedFullCrawl { get; set; }
        public bool IsAutoSuggestionExecutionRequired { get; set; }
        public bool NoUpdateInConfig { get; set; }

    }

    public class DataDictionary
    {
        public string CurrentIndex { get; set; }
        public string PreviousIndex { get; set; }
        public string LastUpdated { get; set; }
    }
}
