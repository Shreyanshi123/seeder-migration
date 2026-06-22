using LiteX.Storage.AmazonS3;
using LiteX.Storage.Azure;
using LiteX.Storage.Core;
using LiteX.Storage.GoogleCloud;
using LiteX.Storage.Kvpbase;
using SearchLego.DataSeeder.Entities;
using System.Collections.Generic;

namespace SearchLego.DataSeeder.Common
{
    public class JobDeatil
    {
        public string Id { get; set; }
        public string IndexType { get; set; }
        public string IndexPrefix { get; set; }
        public string PipelineName { get; set; }
        public bool IsAttachment { get; set; }
        public AttachmentProcessingType AttachmentProcessingType { get; set; }
        public string FilePath { get; set; }
        public string FilePropertyName { get; set; }
        public string CronExpression { get; set; }
        public string SQLQuery { get; set; }
        public string ProcParameter_LastUpdated { get; set; }
        public bool Enabled { get; set; }
        public string ColumnsToSplit { get; set; }
        public string SplitSeperator { get; set; }
        public string ExcludeFieldsFromSuggestion { get; set; }
        public int IngestBatchSize { get; set; }
        public int TabSequence { get; set; }
        public StorageProviderType FileStorageType { get; set; }
        public JobType JobType { get; set; }
        public string ExcludeFieldsToPerformSearch { get; set; }

        public string ColumnsToTextFromHtml { get; set; }
        public bool IsExcelPreview { get; set; }
        public int PreviewBatchSize { get; set; }
        public string TabClientList { get; set; }
        public bool cognitiveEnabled { get; set; } = false;
        public JobProcessType CognitiveProcessType { get; set; }
        public bool IsAutoSuggested { get; set; }
        public int SuggestionsGramLimit { get; set; }
        public bool EnableSingleWordSuggestion { get; set; }

        public string IgnoreCharacters { get; set; }

        public bool ExcludeAlphaNumericSuggestion { get; set; }
    }

    public class ApplicationConfig
    {
        public ServerType ServerType { get; set; }
        public string ElasticUri { get; set; }
        public string DataConnectionString { get; set; }
        public IList<JobDeatil> JobDeatil { get; set; }
        public string DefaultValue { get; set; }
        public string SLBConfigConnectionString { get; set; }
        public string SLBConfigDataBase { get; set; }
        public string SLBDocDb { get; set; }
        public SearchUISetting SearchUISetting { get; set; }
        public bool EnableUISetting { get; set; }
        public EncryptionSetting EncryptionSetting { get; set; }
        public string AllowedClientList { get; set; }
        public DocumentPreviewSetting DocumentPreviewSetting { get; set; }
        public bool IsAzureVaultEnabled { get; set; }
        public bool UseElasticCloud { get; set; }
        public string ElasticUserName { get; set; }
        public string ElasticPassword { get; set; }
        public AzureVaultKeys AzureVaultKeys { get; set; }
        public bool EnableJobHistory { get; set; } 
        public string TenantType { get; set; }
        public static IEnumerable<Tenant> TenantCollections { get; set; } = new List<Tenant>();
        public ExcelFileBasedTagging ExcelBasedTagging { get; set; }
        public string TenantDBName { get; set; }
    }
    /// <summary>
    /// This enum is used for segrigate the job type, is it pdf generation or data ingesation.
    /// </summary>
    public enum JobType
    {
        PDFGenerate,
        DataIngest,
        CognitiveDataIngestion,
        CleanUpRepository
    }
    /// <summary>
    /// This enum is used for segrigate the attachment process type, is it pipeline or preview(pdf generation process in existing job)
    /// </summary>
    public enum AttachmentProcessingType
    {
        Pipeline = 0,
        Preview = 1
    }
    public class DocumentPreviewSetting
    {
        public bool Enabled { get; set; }
        public int DocumentMaxSizeInMB { get; set; }
        public string IncludeFileType { get; set; }
        public Aspose Aspose { get; set; }
        public IText IText { get; set; }     
        public LibreOfficeSetting LibreOfficeSetting { get; set; }
        public string DocumentTextExtactionLibrary { get; set; }
    }
    public class Aspose
    {
        public bool Enabled { get; set; }
        public string WordLicPath { get; set; }
        public string SlideLicPath { get; set; }
        public string PdfLicensePath { get; set; }
        
    }
    public class IText
    {
        public bool Enabled { get; set; }
        public string PdfLicensePath { get; set; }

    }    
    public class LibreOfficeSetting
    {
        public string LibreOfficePath { get; set; }
        public string PdfDestinationFilePath { get; set; }
        public Dictionary<string, string> Commands { get; set; }
    }
    public class EncryptionSetting
    {
        public bool Enabled { get; set; }
        public string Key { get; set; }
        public string FieldName { get; set; }
    }
    public class SearchUISetting
    {
        public string SQLQuery { get; set; }
        public string ProcParameterName { get; set; }

    }
    public class ParametersValue
    {
        public IList<Jobs> jobs { get; set; }
    }
    public class Jobs
    {
        public string Id { get; set; }
        public string LastUpdatedDate { get; set; }
        public string LastConfigUpdated { get; set; }
        public bool IsForcedToFullCrawl { get; set; }
        public string IndexName { get; set; }
        public string FullCrawlExecutionTime { get; set; }
    }


    public class Azure
    {
        public bool Enabled { get; set; }
        public AzureBlobStorageConfig AzureBlobStorageConfig { get; set; }
    }
    public class Amazon
    {
        public bool Enabled { get; set; }
        public AmazonS3Config AmazonS3Config { get; set; }
    }
    public class Google
    {
        public bool Enabled { get; set; }
        public GoogleCloudStorageConfig GoogleCloudStorageConfig { get; set; }
    }
    public class Kvpbase
    {
        public bool Enabled { get; set; }
        public KvpbaseStorageConfig KvpbaseStorageConfig { get; set; }
    }

    public class AzureVaultKeys
    {
        public string SqlDataConnectionSecretName { get; set; }
        public string MongoConnectionSecretName { get; set; }
        public string ElasticUri { get; set; }
        public string ElasticUserName { get; set; }
        public string ElasticPassword { get; set; }
        public string AzureBlobStorageConnectionString { get; set; }
        public string AmazonAwsSecretAccessKey { get; set; }
        public string GoogleJsonAuthPath { get; set; }
        public string KvpbaseApiKey { get; set; }
    }

    public class CloudConstants
    {
        public const string AZURE = "Azure";
        public const string AMAZON = "Amazon";
        public const string GOOGLE = "Google";
        public const string KVPBASE = "Kvpbase";
    }
    public class TenantCollection
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
    }
    public class ExcelFileBasedTagging
    {
        public bool Enabled { get; set; }
        public string ExcelFilePath { get; set; }
    }
}
