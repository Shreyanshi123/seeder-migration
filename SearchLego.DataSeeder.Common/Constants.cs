using System;
using System.Collections.Generic;
using System.Text;

namespace SearchLego.DataSeeder.Common
{
    public class Constants
    {
        //Common
        public const string CLIENT_ID = "clientId";
        public const string PROJECT_ID = "projectId";
        public const string IS_NER_PROCESSED = "isNERProcessed";
        public const string EQUAL_TO = "=";
        public const string IS_PROCESSED = "isProcessed";
        public const string IS_DOC_SUMMARY_PROCESSED = "isDocSummaryProcessed";

        // Suggestions
        public const string SUGGESTIONS = "suggestions";
        public const string UNDERSCORE = "_";
        public const string IS_AUTO_SUGGESTED = "isAutoSuggested";
        public const string CONTENT = "Content";
        public const string AUTO_COMPLETE = "_autocomplete";
        // Url 
        public const string NER_URL = "cognitive/ner/return_entities";
        public const string AUTO_SUGGESTIONS_URL = "cognitive/autosuggestion/return_suggestions";
        public const string VECTOR_REPRESENATION_URL = "cognitive/related_entities/create_vector_represenation";
        public const string QUERY_EMBEDDINGS_URL = "cognitive/related_search/create_query_embeddings";
        public const string DATA_SUMMARY = "cognitive/extractive_summarization/return_summary";

        // Model Ingest Constants
        public const string ML_MODEL_CONFIG_JOBNAME = "jobName";
        public const string ML_MODEL_CONFIG_ISLATEST = "isLatest";
        public const string IS_LATEST = "isLatest";
        public const string NER_ENTITES = "NEREntities";
        public const string SIZE = "10000";
        public const string LOCATION = "location";
        public const string ORGANIZATION = "organization";
        public const string DOC_SUMMARY = "docSummary";

        // Auto-Suggestions Status
        // Initial Status is the first or default status for IsAutoSuugestedFlag
        public const string INITIAL_STATE = "InitialState";
        // Processed Status is for Sucessfully generated Auto-suggestions 
        public const string PROCESSED = "Processed";
        // Incomplete Status is the first time fail for run the Auto-Suggestions
        public const string INCOMPLETE = "InComplete";
        // UnProcessed Status is the 2nd time fail to run the Auto-Suggestions
        public const string UNPROCESSED = "UnProcessed";

        public const string EXCLUDE_ALPHANUMERIC_SUGGESTION = "ExcludeAlphaNumericSuggestion";

        //Data Dictionary
        public const string DATA_DICTIONARY = "data_dictionary";
        // Status
        public const string SUCCESS = "SUCCESS";
        public const string FAILED = "FAILED";

        //JobName
        public const string MODEL_INGEST = "ModelIngest";
        public const string DICTIONARY = "Dictionary";
    }
    public enum TextExtractionLibrary
    {
        Aspose,
        IText
    }
}
