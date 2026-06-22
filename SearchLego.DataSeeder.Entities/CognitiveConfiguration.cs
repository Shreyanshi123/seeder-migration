using MongoDB.Bson;
using System;
using System.Collections.Generic;

namespace SearchLego.DataSeeder.Entities
{
    public class FeatureConfiguration
    {
        public string _id { get; set; }
        public string baseApi { get; set; }
        public string featureType { get; set; }
        public List<IndexType> indexTypes { get; set; }
    }

    public class IndexType
    {
        public string type { get; set; }
        public List<string> includeFields { get; set; }
        public int batchSize { get; set; }
        public bool isDocument { get; set; }
        public int suggestionGramLimit { get; set; }
        public string sourceType { get; set; }
        public string sourceName { get; set; }
        public string sourceUrl { get; set; }
        public string indexId { get; set; }
        public string sourcePrefix { get; set; }
        public string tenantId { get; set; }
        public bool useTenantSource { get; set; }
        public bool useTenantSourceStorage { get; set; }
        public string fileAccessibility { get; set; }
        public string tagMetaDataFileName { get; set; }
        public bool enableExcelbaseTagging { get; set; }

        public bool isActive { get; set; }

    }

    public class JobClientFeatureMapping
    {
        public string _id { get; set; }
        public string featureType { get; set; }
        public List<string> clientIds { get; set; }
    }

    public class JobTracking
    {
        public string _id { get; set; }
        public string clientId { get; set; }
        public bool enabled { get; set; }
        public List<string> indexTypes { get; set; }
        public List<Job> jobs { get; set; }
    }

    public class Job
    {
        public string name { get; set; }
        public string status { get; set; }
        public string lastExecutedTime { get; set; }
        public bool enabled { get; set; }
        public bool isForcedExecution { get; set; }
        public AdditionalSetting additionalSetting { get; set; }
    }

    public class AdditionalSetting
    {
        public List<string> projectIds { get; set; }
        public string currentIndex { get; set; }
        public string previousIndex { get; set; }
        public List<ExecutionInfo> sourceExecutionInfos { get; set; }

    }
    public class ExecutionInfo
    {
        public string sourceName { get; set; }
        public string sourceUrl { get; set; }
        public string sourceType { get; set; }
        public bool isForcedExecution { get; set; }
        public string lastExecutedTime { get; set; }
    }

    public class ModelConfig
    {
        public Guid _id { get; set; }
        public int clientId { get; set; }
        public int projectId { get; set; }
        public double run_time { get; set; }
        public string pickle_location { get; set; }
        public string vector_index_location { get; set; }
        public string pickle_name { get; set; }
        public string vector_index_name { get; set; }
        public bool isLatest { get; set; }
        public bool status { get; set; }
        public string jobName { get; set; }
    }

    public class JobHistory
    {
        public string clientId { get; set; }
        public string jobId { get; set; }
        public string featureType { get; set; }
        public string lastExecutedTime { get; set; }
        public string status { get; set; }
        public string noOfRecordsUpdated { get; set; }
        public string jobHistoryMessage { get; set; }
    }

    public enum JobProcessType
    {
        NEREntities,
        NERCustomEntities,
        DataDictionary,
        AutoSuggestion,
        RelatedSearch,
        PeopleAlsoSearch,
        PDFGenerate,
        DataSummary,
        RssFeed,
        FileIngestion
    }

    public enum ModelType
    {
        RelatedSearch,
        PeopleAlsoSearch
    }

    public class JobProcessInfo
    {
        public string ClientId { get; set; }

        public bool isForcedExecution { get; set; }
        public string LastExecutedTime { get; set; }
        public List<string> IndexTypes { get; set; }
        public FeatureConfiguration FeatureConfigurationSetting { get; set; }
        public AdditionalSetting ClientAdditionalSetting { get; set; }
    }

    public class JobTrackingUpdateInfo
    {
        public bool Status { get; set; }
        public string CurrentIndex { get; set; }
        public string PreviousIndex { get; set; }
        public bool SkipUpdate { get; set; }
        public int NoOfUpdatedRecords { get; set; }
        public DateTime LastExecutedTime { get; set; }
        public List<ExecutionInfo> sourceExecutionInfos { get; set; }

    }
    public class NerEntityRuler
    {
        public ObjectId _id { get; set; }
        public int clientId { get; set; }
        public int projectId { get; set; }
        public string searchTerm { get; set; }
        public string entity { get; set; }
        public string pattern { get; set; }
        public string pattern_Id { get; set; }
        public bool isCustomNerProcessed { get; set; }
    }
    public enum FileAccessibilityType
    {
        Private=1, // Only me
        Public=2,
        Group=3
        
    }
    
}
