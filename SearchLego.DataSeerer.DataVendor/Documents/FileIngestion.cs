using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using LiteX.Storage.Core;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Elastic;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Xml;
using System.IO;
using SearchLego.DataSeeder.CloudStorage;
using SearchLego.DataSeeder.StorageService;
using SearchLego.DataSeeder.FileConvert;
using System.Web;

namespace SearchLego.DataSeerer.Integration.Documents
{
    public class FileIngestion : IFileIngestion
    {
        private readonly ILogger<dynamic> _logger;
        private ApplicationConfig applicationConfig;
        private IElasticIndexBuilder elasticIndexBuilder;
        private readonly ILiteXStorageProviderFactory _liteXStorageProviderFactory;
        private IElasticConnector elasticConnector;
        public FileIngestion(ILogger<dynamic> logger, ApplicationConfig _applicationConfig, IElasticIndexBuilder _elasticIndexBuilder, IElasticConnector _elasticConnector, ILiteXStorageProviderFactory liteXStorageProviderFactory)
        {
            _logger = logger;
            applicationConfig = _applicationConfig;
            elasticIndexBuilder = _elasticIndexBuilder;
            elasticConnector = _elasticConnector;
            _liteXStorageProviderFactory = liteXStorageProviderFactory;
        }


        public string GetFileFormat(string fileExten)
        {
            var ext = fileExten?.ToLower();
            switch (ext)
            {
                case "pdf": return "PDF";
                case "txt": return "TEXT";
                case "xls":
                case "xlsx":
                    {
                        return "EXCEL";
                    }
                case "ppt":
                case "pptx":
                    {
                        return "POWERPOINT";
                    }
                case "doc":
                case "docx":
                    {
                        return "WORD";
                    }
                default: return "";
            }
        }

        public string resolveSourceType(string sourceType)
        {
            var type = (sourceType ?? "").ToLower();
            switch (type)
            {
                case "aws": return StorageTypes.AWS.ToString();
                case "network": return StorageTypes.FileSystem.ToString();
                default: return sourceType;
            }
        }

        public JobTrackingUpdateInfo IngestFiles(string jobId, JobProcessInfo jobProcessInfo, JobDeatil jobDetail)
        {
            _logger.LogInformation($"Execution started for CognitiveProcessType ({JobProcessType.FileIngestion.ToString()})");

            string dir = jobProcessInfo.FeatureConfigurationSetting.baseApi;
            string clientId = jobProcessInfo.ClientId;
            StorageProviderType providerType = jobDetail.FileStorageType;
            List<ExecutionInfo> update_executionInfos = new List<ExecutionInfo>();

            if (jobProcessInfo.FeatureConfigurationSetting != null && jobProcessInfo.FeatureConfigurationSetting.indexTypes != null && jobProcessInfo.FeatureConfigurationSetting.indexTypes.Count > 0)
            {
                var indexTypes = jobProcessInfo.FeatureConfigurationSetting.indexTypes?.Where(i => i.tenantId == clientId);
                _logger.LogInformation($"## Fileingestion Feature Configuration:  {jobId}");
                if (indexTypes != null)
                {
                    foreach (var type in indexTypes)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(type.sourceUrl))
                            {
                                string sourceType = type.sourceType;
                                string sourceId = type.indexId;
                                string sourceName = type.sourceName;
                                string sourceUrl = type.sourceUrl;
                                string sourceConfig = sourceUrl;
                                string folderOrPrefix = type.sourcePrefix ?? "";
                                DateTime sourceLastExecutedAt = DateTime.MinValue;
                                bool isForcedExecution = false;
                                string prev_time = string.Empty;
                                bool useTenantSourceStorage = type.useTenantSourceStorage;
                                string fileAccessibility = type.fileAccessibility;
                                string tagFileName = type.tagMetaDataFileName;
                                bool enableExcelbaseTagging = type.enableExcelbaseTagging;

                                if (jobProcessInfo.ClientAdditionalSetting != null && jobProcessInfo.ClientAdditionalSetting.sourceExecutionInfos != null )

                                {
                                    //if (applicationConfig.TenantType.Equals(TenantConnectionType.Multi.ToString()))
                                    //{
                                    //    folderOrPrefix = ApplicationConfig.TenantCollections.Where(t => t.TenantId == clientId)?.Any() ?? false ? !isTenantSourceFile ? clientId : folderOrPrefix : folderOrPrefix;
                                    //}

                                    var executionInfo = jobProcessInfo.ClientAdditionalSetting.sourceExecutionInfos.Where(exi => exi.sourceUrl == sourceUrl).FirstOrDefault();
                                    if (executionInfo != null)
                                    {
                                        DateTime.TryParse(executionInfo.lastExecutedTime, out sourceLastExecutedAt);
                                        isForcedExecution = executionInfo.isForcedExecution;
                                        prev_time = executionInfo.lastExecutedTime;
                                    }
                                }
                                int count=0;
                                if (type.isActive)
                                {
                                    _logger.LogInformation($"## Fileingestion Process storage Files");

                                    count = ProcessStorageFiles(clientId, sourceType, sourceName, folderOrPrefix, sourceConfig, isForcedExecution, sourceLastExecutedAt,
                                                                providerType, sourceId, useTenantSourceStorage, fileAccessibility, tagFileName, enableExcelbaseTagging);
                                }
                                
                                update_executionInfos.Add(new ExecutionInfo
                                {
                                    isForcedExecution = false,
                                    lastExecutedTime = count > 0 ? DateTime.Now.ToString() : prev_time,
                                    sourceName = sourceName,
                                    sourceType = sourceType,
                                    sourceUrl = sourceUrl
                                });
                                _logger.LogInformation($"successfully uploaded file count: {count} JobId: {jobId} ClientId: {clientId} CognitiveProcessType: ({JobProcessType.FileIngestion.ToString()})");

                            }

                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"invalid operation - job id : {jobId} clientId: {clientId} CognitiveProcessType: {JobProcessType.FileIngestion.ToString()}, Fileingest exception in loop, {ex.Message}, {ex.StackTrace}");

                        }
                    }
                }

            }
            var jobTrackingUpdateInfo = new JobTrackingUpdateInfo
            {
                SkipUpdate = false,
                Status = true,
                LastExecutedTime = DateTime.Now,
                sourceExecutionInfos = update_executionInfos,
            };
            return jobTrackingUpdateInfo;
        }

        private int ProcessStorageFiles(string clientId, string sourceType,
            string sourceName, string folderOrPrefix, string sourceConfig, bool isForcedExecution,
            DateTime source_lastExecuted, StorageProviderType providerType, string sourceId, bool useTenantSourceStorage,
            string fileAccessibility, string tagFileName, bool enableExcelbasedTagging)
        {
            _logger.LogInformation($"Process to start File storage for ClientId: {clientId} CognitiveProcessType:{JobProcessType.FileIngestion.ToString()} ");
            int count = 0; string excelTagFileName = string.Empty;
            Stream fileStream = null;
            StorageTypes spt = (StorageTypes)Enum.Parse(typeof(StorageTypes), resolveSourceType(sourceType), true);
            StorageProvider storageProvider = new StorageProvider(spt);
            var storageService = storageProvider.StorageService(sourceConfig);
           

            if (storageService != null)
            {
                var files = storageService.GetFiles(folderOrPrefix, isForcedExecution ? DateTime.MinValue : source_lastExecuted);

                if (enableExcelbasedTagging && !string.IsNullOrEmpty(tagFileName))
                {
                    excelTagFileName = files.Where(f => f.FileName.Contains(tagFileName)).Select(file => file.FileName)?.FirstOrDefault(); //applicationConfig.ExcelBasedTagging.ExcelFilePath
                    if (!string.IsNullOrEmpty(excelTagFileName))
                        fileStream = storageService.GetFileStream(excelTagFileName);
                }

                var validFiles = files?.Where(x => (isForcedExecution ? true : x.LastModified > source_lastExecuted) && !String.IsNullOrEmpty(GetFileFormat(x.FileExtension)) &&
                                 !x.FileName.Equals(excelTagFileName));
                foreach (var file in validFiles)
                {
                    try
                    {

                        //upload to cloud
                        long docLenght = 0;
                        if (useTenantSourceStorage)
                        {
                            _logger.LogInformation($"## Fileingestion access from tenant source cloud");
                            docLenght = GetStreamFileLength(storageService, file);
                        }
                        else
                        {
                            _logger.LogInformation($"## Fileingestion upload to cloud");
                            docLenght = uploadToCloud(storageService, file, providerType, clientId);
                        }

                        //insert into mongo
                        if (docLenght > 0)
                        {
                            file.FileSize = docLenght;
                            var document = GetDocumentObj(file, sourceType, sourceName, clientId, sourceId, useTenantSourceStorage, fileAccessibility, fileStream);
                            InsertIntoMongo(document, clientId);
                            _logger.LogInformation($"document information saved to mongo for ClientId: {clientId} CognitiveProcessType:{JobProcessType.FileIngestion.ToString()} ");

                        }
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"invalid operation while uploding file to cloud., {ex.Message}, {ex.StackTrace}");
                        throw;
                    }
                }
            }
            return count;
        }

        private long uploadToCloud(IStorageService service, FileDescription file, StorageProviderType providerType, string clientId)
        {
            long length = 0;
            using (Stream fs = service.GetFileStream(file.FileName))
            {
                length = fs.Length;
                var fileStorage = new FileFromCloudStorage(_liteXStorageProviderFactory);
                var cloudFileName = file.id + "." + file.FileExtension;

                if (!string.IsNullOrEmpty(clientId))
                    cloudFileName = Path.Combine(clientId, cloudFileName);
                //insert into cloud
                fileStorage.UploadStreamToCloud(providerType, cloudFileName, fs);
                fs.Close();
            }
            return length;
        }
        private long GetStreamFileLength(IStorageService service, FileDescription file)
        {
            long length = 0;
            using (Stream fs = service.GetFileStream(file.FileName))
            {
                length = fs.Length;
                fs.Close();
            }
            return length;
        }
        private string getFullFolderPathFromFilePath(string filePath)
        {
            List<dynamic> files = new List<dynamic>();

            var folderName = Path.Join("", filePath).Replace("\\", "/").Split("/").SkipLast(1);
            return string.Join("/", folderName);

        }

        private object GetDocumentObj(FileDescription file, string sourceType, string sourceName, string clientId, string sourceId, bool useTenantSourceStorage, string fileAccessibility, Stream stream)
        {
            string folderName = getFullFolderPathFromFilePath(file.FolderName);
            object tagMetaData = null; string _fileName = String.Empty;

            if (stream != null)
            {
                ExcelBasedTagging excelBasedTagging = new ExcelBasedTagging(_logger);
                string relativePath = new Uri(file.Url).AbsolutePath;

                dynamic dynamicObject = excelBasedTagging.GetTagMetaData(HttpUtility.UrlDecode(relativePath), file.Url, stream);

                if (dynamicObject != null)
                {

                    var fileName = ((IEnumerable<KeyValuePair<string, object>>)dynamicObject).Where(d => d.Key == "File_Name").Select(k => k.Value)!.FirstOrDefault();
                    if (fileName != null)
                        _fileName = ((string[])fileName)[0];

                    tagMetaData = dynamicObject;
                }


            }
            var moretags = new
            {
                moretags = folderName.Split(@"/", StringSplitOptions.RemoveEmptyEntries)
            };

            var tags = tagMetaData != null ? tagMetaData : moretags;

            return new
            {
                _id = file.id,
                filePath = file.Url,
                fileName = !string.IsNullOrEmpty(_fileName) ? Path.GetFileNameWithoutExtension(_fileName) : Path.GetFileNameWithoutExtension(file.FileName),
                fileExtension = file.FileExtension,
                fileFormat = GetFileFormat(file.FileExtension),
                fileSize = file.FileSize,
                folderName = folderName,
                metadata = tags,
                sourceType = sourceType,
                sourceName = sourceName,
                originalFileName = file.FileName,
                createdOn = DateTime.Now,
                lastUpdated = DateTime.Now,
                createdBy = "FileSync",
                createdByEmail = "FileSync@evalueserve.com",
                tenantId = clientId,
                isActive = true,
                fileAccessibility = !string.IsNullOrEmpty(fileAccessibility) ? fileAccessibility : "2",
                useTenantSourceStorage = useTenantSourceStorage,
                sourceId = sourceId,
            };
        }

        private bool InsertIntoMongo(dynamic data, string clientId)
        {
            IMongoDatabaseFactory mongoDatabaseFactory = new MongoDatabaseFactory();
            IMongoConfigFactory _modelConfigFactory = new MongoConfigUpdate(mongoDatabaseFactory.CreateDatabase(applicationConfig, clientId, applicationConfig.SLBDocDb), MongoStaticName.Documents);

            var newItem = BsonSerializer.Deserialize<BsonDocument>(JsonConvert.SerializeObject(data, new JavaScriptDateTimeConverter()));
            if (newItem != null)
                _modelConfigFactory.Update(newItem);
            return true;
        }

        private dynamic IsExistingDocument(string filePath)
        {
            IMongoDatabaseFactory mongoDatabaseFactory = new MongoDatabaseFactory();
            IMongoConfigFactory _modelConfigFactory = new MongoConfigUpdate(mongoDatabaseFactory.CreateDatabase(applicationConfig, null, applicationConfig.SLBDocDb), MongoStaticName.Documents);
            var doc = _modelConfigFactory.GetByFieldValue("filePath", filePath);
            return new
            {
                isExistingDocument = doc != null ? true : false,
                doc = doc
            };
        }
    }
}
