using Elasticsearch.Net;
using HtmlAgilityPack;
using KvpbaseSDK;
using LiteX.Storage.Core;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SearchLego.DataSeeder.CloudStorage;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.FileConvert;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.NER;
using SearchLego.DataSeeder.StorageService;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace SearchLego.DataSeeder.Elastic
{
    public class ElasticIngest : IElasticIngest
    {
        private readonly IElasticConnector _iElasticConnector;
        private readonly IElasticIndexBuilder _iElasticIndexBuilder;
        private readonly ILogger<dynamic> _logger;
        private readonly bool? _useNewerElastic = null;
        private readonly IExtractTextFromFile _extractTextFromFile;
        private readonly IConvertFileToPDF _convertFileToPDF;
        private readonly ILiteXStorageProviderFactory _liteXStorageProviderFactory;

        public ElasticIngest(ILogger<dynamic> logger, IElasticConnector iElasticConnector, IElasticIndexBuilder iElasticIndexBuilder,
            IExtractTextFromFile extractTextFromFile, IConvertFileToPDF convertFileToPDF, ILiteXStorageProviderFactory liteXStorageProviderFactory)
        {
            _iElasticConnector = iElasticConnector;
            _iElasticIndexBuilder = iElasticIndexBuilder;
            _logger = logger;
            _convertFileToPDF = convertFileToPDF;
            _extractTextFromFile = extractTextFromFile;
            _liteXStorageProviderFactory = liteXStorageProviderFactory;
        }
        public bool BulkUpdateDocument(string jsonData, JobDeatil jobDetail, string indexName, IEnumerable<DataRow> configFieldsByClient, ClientConfiguration clientConfig, out int recordsUpdated)
        {
            bool _flag = false;
            recordsUpdated = 0;
            try
            {
                // _logger.LogInformation("Start Bulk Ingest.");
                var listofItems = JsonConvert.DeserializeObject<List<object>>(jsonData).ToList();
                int IngestBatchSize;
                int totalNoOfBatchCount = jobDetail.IngestBatchSize > listofItems.Count ? 1 : Convert.ToInt32(Math.Ceiling((decimal)listofItems.Count / (decimal)jobDetail.IngestBatchSize));
                IngestBatchSize = jobDetail.IngestBatchSize > listofItems.Count ? listofItems.Count : jobDetail.IngestBatchSize;
                int recordCount = 0;

                for (int i = 0; i < totalNoOfBatchCount; i++)
                {

                    IngestBatchSize = i == (totalNoOfBatchCount - 1) ? (listofItems.Count - recordCount) : IngestBatchSize;
                    var listelasticitems = new List<dynamic>();

                    for (int j = 0; j < IngestBatchSize; j++)
                    {
                        var item = listofItems[recordCount];
                        var isactive = item.GetType().GetProperty("IsActive");
                        var resultItem = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(item));
                        if (!resultItem.ContainsKey("id"))
                        {
                            _logger.LogError($"Initial_Validation - {jobDetail.IndexType} id not found in data field");
                            break; ;
                        }
                        if (!resultItem.ContainsKey("Title"))
                        {
                            _logger.LogError($"Initial_Validation - {jobDetail.IndexType} Title not found in data field");
                            break;
                        }
                        if (resultItem.ContainsKey("IsActive"))
                        {
                            bool isActive = true;
                            string id = resultItem["id"].ToString();
                            string s_isActive = Convert.ToString(resultItem["IsActive"]);
                            if (bool.TryParse(s_isActive, out isActive))
                            {
                                if (isActive == false)
                                {
                                    listofItems.Remove(item);
                                    try
                                    {
                                        _iElasticConnector._elasticClient.Delete<ElasticDocument>(id, d => d.Index(indexName));
                                    }
                                    catch (ElasticsearchClientException ex)
                                    {
                                        _logger.LogError($"Record Deletion - Record [{id}] deletion failed exception message:  {ex.Message}  {ex.StackTrace}");
                                    }
                                    try
                                    {
                                        _iElasticConnector._elasticClient.Delete<ElasticDocument>(id, d => d.Index(indexName + Constants.AUTO_COMPLETE));
                                    }
                                    catch (ElasticsearchClientException ex)
                                    {
                                        _logger.LogError($"Record Deletion - Record [{id}] deletion failed exception message:  {ex.Message}  {ex.StackTrace}");
                                    }
                                    continue;
                                }
                            }
                            else
                            {
                                _logger.LogError($"Invalid Value - Record [{id}] has invalid value for [isActive] field: {s_isActive}");
                            }
                        }
                        resultItem[Constants.IS_DOC_SUMMARY_PROCESSED] = Constants.INITIAL_STATE;
                        resultItem[Constants.IS_NER_PROCESSED] = Constants.INITIAL_STATE;
                        resultItem[Constants.IS_AUTO_SUGGESTED] = Constants.INITIAL_STATE;
                        //if(jobDetail.cognitiveEnabled)
                        //   NerProcess(resultItem, clientConfig);
                        if (!string.IsNullOrEmpty(jobDetail.ColumnsToSplit))
                            SplitColumnValue(resultItem, jobDetail);
                        if (configFieldsByClient != null)
                            SplitDynamicFieldValue(resultItem, configFieldsByClient, jobDetail.SplitSeperator);
                        if (!string.IsNullOrEmpty(jobDetail.ColumnsToTextFromHtml))
                            GetTextFromHtml(resultItem, jobDetail.ColumnsToTextFromHtml, jobDetail.IgnoreCharacters);
                        if (resultItem.ContainsKey("Tag"))
                            resultItem["Tag"] = Convert.ToString(resultItem["Tag"]).Replace(jobDetail.SplitSeperator, ",");
                        if (jobDetail.EnableSingleWordSuggestion)
                        {
                            resultItem[Constants.SUGGESTIONS] = new ElasticDocument() { Suggestions = CreateSuggestions(resultItem, jobDetail) }.Suggestions.Where(w => w.Input.Trim().Length > 2);
                            resultItem[Constants.IS_AUTO_SUGGESTED] = Constants.PROCESSED;
                        }
                        listelasticitems.Add(new { update = new { _index = indexName, _id = resultItem["id"] } });
                        listelasticitems.Add(new { doc = resultItem, doc_as_upsert = true });
                        recordCount++;
                        item = null;
                        resultItem = null;
                    }

                    var result = _iElasticConnector._lowLevelClient.Bulk<StringResponse>(PostData.MultiJson(listelasticitems));
                    listelasticitems = null;
                    if (result.Success)
                    {
                        recordsUpdated = listofItems.Count;
                        Console.WriteLine(string.Format("{0} record ingest out of {1} for index : {2}", recordCount, listofItems.Count, indexName));
                        //  _logger.LogInformation("Bulk Ingest has been done.");
                        _flag = true;
                    }
                    else
                    {
                        _logger.LogInformation($"Initial_Validation: Fail to ingest for job {jobDetail.IndexType} {result.ApiCall.DebugInformation} ");
                        //   _logger.LogWarning($"Bulk Ingest has been failed  message:  { result.DebugInformation}  { result.AuditTrail}");
                        _flag = false;
                    }
                }
                listofItems = null;

            }
            catch (ElasticsearchClientException e)
            {
                _flag = false;
                _logger.LogWarning($"Initial_Validation - Bulk Ingest has been failed exception message:  {e.Message}  {e.StackTrace}");
            }
            catch (Exception ex)
            {
                _flag = false;
                _logger.LogError($"Initial_Validation - Bulk Ingest has been failed exception message:  {ex.Message}  {ex.StackTrace}");
            }
            return _flag;
        }
        public bool BulkUpdateWithAttachment(string jsonData, JobDeatil jobDetail, string indexName, ApplicationConfig jobsDetail, IEnumerable<DataRow> configFieldsByClient, ClientConfiguration clientConfig, IMongoConfigFactory mongoConfigFactory, out int recordsUpdated)
        {
            dynamic currentItem = null;
            recordsUpdated = 0;

            bool _flag = false;
            try
            {
                _logger.LogInformation("Start Ingest with attachment.");
                var listofItems = JsonConvert.DeserializeObject<List<IDictionary<string, object>>>(jsonData);

                string pipeLine = (string.IsNullOrEmpty(jobDetail.IndexPrefix) ? "" : jobDetail.IndexPrefix) + jobDetail.PipelineName;
                foreach (var item in listofItems.ToList())
                {
                    if (!item.ContainsKey("id"))
                    {
                        _logger.LogError($"Initial_Validation - {jobDetail.IndexType} id not found in data field");
                        break; ;
                    }

                    if (!item.ContainsKey("Title"))
                    {
                        _logger.LogError($"Initial_Validation - {jobDetail.IndexType} Title not found in data field");
                        break;
                    }
                    _logger.LogInformation($"Start Ingest for {jobDetail.IndexType} id: {item["id"].ToString()}");
                    if (item.ContainsKey("IsActive"))
                    {
                        if (!Convert.ToBoolean(item["IsActive"]))
                        {
                            listofItems.Remove(item);
                            try
                            {
                                _iElasticConnector._elasticClient.Delete<ElasticDocument>(item["id"].ToString(), d => d.Index(indexName));

                            }
                            catch (ElasticsearchClientException ex)
                            {
                                _logger.LogError($"Record Deletion - Record deletion failed exception message:  {ex.Message}  {ex.StackTrace}");
                            }
                            try
                            {
                                _iElasticConnector._elasticClient.Delete<ElasticDocument>(item["id"].ToString(), d => d.Index(indexName + Constants.AUTO_COMPLETE));

                            }
                            catch (ElasticsearchClientException ex)
                            {
                                _logger.LogInformation($"Record has been deleted successfully for {jobDetail.IndexType} id : {item["id"].ToString()}  ");

                            }
                            continue;
                        }
                    }
                    currentItem = item;
                    //if (item[jobDetail?.FilePropertyName].ToString() == "" || jobsDetail.DefaultValue == item[jobDetail?.FilePropertyName].ToString())
                    //continue;
                    Stream fileStream = null;
                    string filePath = string.Empty;
                    if (jobDetail.FileStorageType != StorageProviderType.FileSystem)
                    {
                        try
                        {
                            string clientId = item["clientId"].ToString();
                            object sourceId;
                            bool isSourceIdExist = item.TryGetValue("SourceId", out sourceId);
                            isSourceIdExist = string.IsNullOrEmpty((sourceId ?? "").ToString()) ? false : isSourceIdExist;
                            string fileName = (item[jobDetail?.FilePropertyName] ?? "").ToString();
                            if (jobsDetail.TenantType.Equals(TenantConnectionType.Multi.ToString()) && !isSourceIdExist)
                            {
                                fileName = Path.Combine(clientId, fileName);
                            }
                            var fileFromCloud = new FileFromCloudStorage(_liteXStorageProviderFactory);
                            fileStream = isSourceIdExist ? GetCloudFileStreamFromTenantSourceStorage(fileName, sourceId.ToString(), mongoConfigFactory, jobDetail, fileFromCloud, clientId) :
                                            fileFromCloud.GetStreamFromCloud(fileName, jobDetail.FileStorageType);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"{indexName}:Initial_Validation - Exception occured while dowloading file form cloud for id:  {item["id"].ToString()} and  fileName : {Convert.ToString(item[jobDetail?.FilePropertyName])} {Environment.NewLine} Exception : {ex.Message} ");
                        }
                    }
                    else
                    {
                        filePath = Path.Combine(jobDetail?.FilePath, Convert.ToString(item[jobDetail?.FilePropertyName]));
                        filePath = filePath.Replace("/", @"\");
                        //if (!File.Exists(filePath))
                        //{
                        //    _logger.LogError($"File not found for {jobDetail.IndexType} id: { item["id"].ToString() }   File Path: {filePath} ");
                        //    //continue;
                        //}
                    }
                    List<DocumentPage> lstDocumentPage = new List<DocumentPage>();
                    var document = new ElasticDocument();
                    document.id = item["id"].ToString();
                    bool isPreview = false;
                    string content = string.Empty;
                    long fileSizeKb = 0;

                    if (jobDetail.AttachmentProcessingType == AttachmentProcessingType.Pipeline)
                    {
                        try
                        {
                            if (jobDetail.FileStorageType == StorageProviderType.FileSystem && !File.Exists(filePath))
                                goto Skip;
                            if (jobDetail.FileStorageType != StorageProviderType.FileSystem && fileStream == null)
                                goto Skip;
                            string base64File = string.Empty;
                            try
                            {
                                base64File = Convert.ToBase64String(jobDetail.FileStorageType == StorageProviderType.FileSystem ? File.ReadAllBytes(filePath) : CommonUtility.ConvertStreamToByte(fileStream));
                            }
                            catch (Exception ex)

                            {
                                _logger.LogError("SLBError1 - Exception at coverting to base 64 to string ", ex.Message);
                            }
                            if (fileStream != null)
                                fileStream.Close();
                            document.Content = base64File;
                            var pipelineResponse = _iElasticConnector._elasticClient.Ingest.PutPipeline(pipeLine, p => p.Description("document desc")
                                .Processors(pr => pr.Attachment<ElasticDocument>(a => a.Field(f => f.Content).IndexedCharacters(-1)
                                .TargetField(f => f.Attachment))
                                 .Remove<ElasticDocument>(r => r.Field(f => f.Fields(i => i.Content)))));
                            _logger.LogDebug($"{indexName}: Put into the pipeline for {jobDetail.IndexType} id: {item["id"].ToString()}");
                            var bulkResponse = _iElasticConnector._elasticClient.Bulk(b => b.Index(indexName).Pipeline(pipeLine).Index<ElasticDocument>(i => i.Document(document)).Refresh(Refresh.True));
                            _logger.LogDebug($"{indexName}: Attachment updated into Elastic for {jobDetail.IndexType} id: {item["id"].ToString()}");
                            var response = _iElasticConnector._elasticClient.Search<ElasticDocument>(
                             f => f.Index(indexName).Query(p => p.Match(q => q.Field(r => r.id).Query(document.id))).TypedKeys(_useNewerElastic));
                            var responseDocument = response.Hits.Select(i => new ElasticDocument()
                            {
                                id = i.Source.id,
                                Content = i.Source.Attachment.Content
                            });
                            content = responseDocument.FirstOrDefault()?.Content;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"SLBError - {filePath} Document was not updated", ex.Message);
                        }
                    }


                    else if (jobDetail.AttachmentProcessingType == AttachmentProcessingType.Preview && jobsDetail.DocumentPreviewSetting.Enabled)
                    {
                        FileInfo objFileInfo = new FileInfo(filePath);
                        fileSizeKb = objFileInfo.Length / 1024;
                        int fileSizeInMB = Convert.ToInt32(objFileInfo.Length / (1024 * 1024));
                        if (fileSizeInMB > jobsDetail.DocumentPreviewSetting.DocumentMaxSizeInMB)
                        {
                            _logger.LogError($"{indexName}: Initial_Validation - File size is greater than to configured size for {jobDetail.IndexType} id:  {item["id"].ToString()}");
                            isPreview = false;
                        }
                        else
                        {
                            _logger.LogDebug($"{indexName}: Start process to convert into pdf document for {jobDetail.IndexType} id:  {item["id"].ToString()}");
                            isPreview = true;
                            filePath = ConvertToPDF(jobsDetail, indexName, item["id"].ToString(), filePath, ref isPreview);
                            _logger.LogDebug($"{indexName}: Start process to extract text from pdf for {jobDetail.IndexType} id:  {item["id"].ToString()}");

                            if (jobsDetail.DocumentPreviewSetting.DocumentTextExtactionLibrary.ToLower() == TextExtractionLibrary.IText.ToString().ToLower() && 
                                jobsDetail.DocumentPreviewSetting.IText.Enabled)
                            {
                                // Get text from pdf file page wise using iTextSharp
                                lstDocumentPage = _extractTextFromFile.GetTextFromPdf(filePath);
                            }
                            else if (jobsDetail.DocumentPreviewSetting.DocumentTextExtactionLibrary.ToLower() == TextExtractionLibrary.Aspose.ToString().ToLower() && 
                                jobsDetail.DocumentPreviewSetting.Aspose.Enabled)
                            {
                                // Get text from pdf file page wise using AsposePdf
                                lstDocumentPage = _extractTextFromFile.GetTextFromPdfByAspose(filePath);
                            }

                            _logger.LogDebug($"{indexName}: Extract text from pdf completed for {jobDetail.IndexType} id:  {item["id"].ToString()}");
                            content = string.Join(Environment.NewLine, lstDocumentPage.Select(i => i.Text).ToArray());
                        }

                    }

                Skip:
                    //else
                    //    content = string.Join(Environment.NewLine, lstDoBcumentPage.Select(i => i.Text).ToArray());

                    dynamic resultItem = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(item));
                    //if (jobDetail.cognitiveEnabled)
                    //    NerProcess(resultItem, clientConfig);

                    if (resultItem.ContainsKey(jobDetail.FilePropertyName))
                        resultItem["documentPreviewPath"] = resultItem[jobDetail.FilePropertyName];
                    if (jobsDetail.EncryptionSetting.Enabled)
                        EncryptValue(resultItem, jobsDetail.EncryptionSetting);
                    if (!string.IsNullOrEmpty(jobDetail.ColumnsToSplit))
                        SplitColumnValue(resultItem, jobDetail);
                    if (configFieldsByClient != null)
                        SplitDynamicFieldValue(resultItem, configFieldsByClient, jobDetail.SplitSeperator);
                    if (!string.IsNullOrEmpty(jobDetail.ColumnsToTextFromHtml))
                        GetTextFromHtml(resultItem, jobDetail.ColumnsToTextFromHtml, jobDetail.IgnoreCharacters);

                    if (resultItem.ContainsKey("Tag"))
                        resultItem["Tag"] = Convert.ToString(resultItem["Tag"]).Replace(jobDetail.SplitSeperator, ",");

                    resultItem["isPreview"] = isPreview;
                    resultItem["fileSize"] = fileSizeKb;
                    resultItem["attachment"] = new Attachment();
                    resultItem["Content"] = !string.IsNullOrEmpty(content) ? Regex.Replace(content, @"\s+", " ") : content;
                    resultItem["isProcessed"] = Constants.INITIAL_STATE;
                    resultItem[Constants.IS_DOC_SUMMARY_PROCESSED] = Constants.INITIAL_STATE;
                    resultItem[Constants.IS_NER_PROCESSED] = Constants.INITIAL_STATE;
                    resultItem["DocumentPage"] = lstDocumentPage.ToArray();
                    resultItem[Constants.IS_AUTO_SUGGESTED] = Constants.INITIAL_STATE;
                    if (jobDetail.EnableSingleWordSuggestion)
                    {
                        resultItem[Constants.SUGGESTIONS] = new ElasticDocument() { Suggestions = CreateSuggestions(resultItem, jobDetail) }.Suggestions.Where(w => w.Input.Trim().Length > 2);
                        resultItem["isAutoSuggested"] = Constants.PROCESSED;
                    }
                    var updateResponse = _iElasticConnector._elasticClient.Update<ElasticDocument, object>(new DocumentPath<ElasticDocument>(document.id), u => u.Index(indexName)
                     .Doc(resultItem).DocAsUpsert().Refresh(Refresh.True));
                    if (!updateResponse.IsValid)
                        _logger.LogInformation($"Initial_Validation: Fail to ingest for job {jobDetail.IndexType} id: {item["id"].ToString()}{updateResponse.ApiCall.DebugInformation} ");
                    resultItem = null;
                    _logger.LogInformation($"Ingest done successfully with all attribute for {jobDetail.IndexType} id: {item["id"].ToString()}");
                }
                recordsUpdated = listofItems.Count;
                listofItems = null;
                _flag = true;
            }
            catch (ElasticsearchClientException ex)
            {
                _flag = false;
                _logger.LogError($"Initial_Validation - Bulk Ingest with attachment has been failed exception message:  {ex.Message}  {ex.StackTrace}");
                _logger.LogError($"Initial_Validation - Failed to insert : {JsonConvert.SerializeObject(currentItem)}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Initial_Validation - Bulk Ingest with attachment has been failed exception message:  {ex.Message}  {ex.StackTrace}");
                _flag = false;
            }
            return _flag;
        }
        /// <summary>
        /// This function is used to generate pdf from different source like word,excel and ppt and get content page wise and store into elastic.
        /// </summary>
        /// <param name="jobDetail"></param>
        /// <param name="jobsDetail"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public bool GeneratePDFUpdateDocContentPageWise(JobDeatil jobDetail, ApplicationConfig jobsDetail, string indexName, bool isForcedExecution, IMongoConfigFactory mongoConfigFactory)
        {
            int take = jobDetail.PreviewBatchSize;
            var success = false;
            if (isForcedExecution)
            {
                _iElasticConnector.ElasticConnect(indexName);
                bool res = CommonUtility.UpdateStatusAsInitial(Constants.IS_PROCESSED, _iElasticConnector._elasticClient);
                if (res)
                {
                    _logger.LogInformation($"Pdf Generate Job is set for Initial State for {indexName} ");
                }
            }


            try
            {
                // bool _flag = true;
                Stream fileStream = null;
                string filePath = string.Empty;
                if (indexName != null)
                {
                    // int totalRecord;
                    // int recordProcessedCount=0;
                    // totalRecord =Convert.ToInt32(_iElasticConnector._elasticClient.Count<ElasticDocument>(c => c.Index(indexName)).Count);
                    // while (_flag)
                    {
                        var searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                        {
                            _source = new { },
                            Size = take,
                            query = new
                            {
                                match = new
                                {
                                    isProcessed = new
                                    {
                                        query = Constants.INITIAL_STATE
                                    }
                                }
                            }
                        }));

                        dynamic hitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                        // Added for Run the Failhits once
                        dynamic failHitsResult = null;
                        if (hitsResult != null && hitsResult.hits.Count == 0)
                        {
                            searchResult = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                            {
                                _source = new { },
                                Size = take,
                                query = new
                                {
                                    match = new
                                    {
                                        isProcessed = new
                                        {
                                            query = Constants.INCOMPLETE
                                        }
                                    }
                                }
                            }));
                            failHitsResult = JsonConvert.DeserializeObject<dynamic>(searchResult.Body).hits;
                        }
                        // Ended

                        searchResult = null;
                        //if(hitsResult==null)
                        //    break;
                        if ((hitsResult != null && hitsResult.hits.Count > 0) || (failHitsResult != null && failHitsResult.hits.Count > 0))
                        {
                            IEnumerable<dynamic> list_hits;
                            IEnumerable<dynamic> _iHits = ((IEnumerable<dynamic>)hitsResult.hits).Cast<dynamic>();
                            if (_iHits.Any())
                            {
                                list_hits = hitsResult.hits;
                            }
                            else
                            {

                                list_hits = failHitsResult.hits;
                            }
                            foreach (var result in list_hits)
                            {
                                string id = string.Empty;
                                try
                                {
                                    // recordProcessedCount++;
                                    long fileSizeKb = 0;
                                    bool isPreview = false;
                                    List<DocumentPage> lstDocumentPage = new List<DocumentPage>();
                                    dynamic source = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(result._source));
                                    string fileName = GetValueFromSource(source, "documentPreviewPath");
                                    id = Convert.ToString(GetValueFromSource(source, "id"));
                                    string sourceId = GetValueFromSource(source, "SourceId");

                                    string clientId = Convert.ToString(GetValueFromSource(source, "clientId") ?? "");                                   
                                    if (jobsDetail.TenantType.Equals(TenantConnectionType.Multi.ToString()) && string.IsNullOrEmpty(sourceId))
                                    {
                                        fileName = Path.Combine(clientId, fileName);
                                    }

                                    string content = string.Empty;
                                    source = null;
                                    if (string.IsNullOrEmpty(fileName))
                                    {
                                        _logger.LogError($"{indexName}: Initial_Validation - file name not found in elastic data for id:  {id} and  fileName : {fileName} ");
                                        continue;
                                    }
                                    if (jobDetail.FileStorageType != StorageProviderType.FileSystem)
                                    {
                                        _logger.LogDebug($"{indexName}: File downloading from cloud for document id:  {id}");
                                        // Get file from cloud storage as stream from different source like amazon, azure
                                        try
                                        {
                                            var obj = new FileFromCloudStorage(_liteXStorageProviderFactory);
                                            using (fileStream = !string.IsNullOrEmpty(sourceId) ? GetCloudFileStreamFromTenantSourceStorage(fileName, sourceId, mongoConfigFactory, jobDetail, obj, clientId) :
                                                obj.GetStreamFromCloud(fileName, jobDetail.FileStorageType))
                                            {
                                                if (fileStream == null || fileStream.Length == 0)
                                                    continue;
                                                filePath = Path.Combine(Path.GetTempPath(), Path.GetFileName(fileName));
                                                filePath = filePath.Replace("/", @"\");
                                                CommonUtility.CopyStream(fileStream, filePath); // Save file in temp directory 
                                                _logger.LogDebug($"{indexName}: File downloading from cloud completed for document id:  {id} and file path : {filePath}");
                                            }
                                            obj = null;

                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError($"{indexName}: Initial_Validation - Exception occured while dowloading file form cloud for id:  {id} and  fileName : {fileName} {Environment.NewLine} Exception : {ex.Message} ");
                                            continue;
                                        }


                                    }
                                    else if (jobDetail.FileStorageType == StorageProviderType.FileSystem)
                                    {
                                        filePath = fileName.Replace("/", @"\");
                                        filePath = Path.Combine(jobDetail.FilePath, filePath);
                                    }

                                    if (jobDetail.AttachmentProcessingType == AttachmentProcessingType.Preview)
                                    {
                                        if (!File.Exists(filePath))
                                        {
                                            SetProcessedFailedStatusToDoc(id, indexName);
                                            continue;
                                        }
                                        FileInfo objFileInfo = new FileInfo(filePath);
                                        fileSizeKb = objFileInfo.Length / 1024;
                                        int fileSizeInMB = Convert.ToInt32(objFileInfo.Length / (1024 * 1024));
                                        if (fileSizeInMB > jobsDetail.DocumentPreviewSetting.DocumentMaxSizeInMB)
                                        {
                                            _logger.LogError($"{indexName}: Initial_Validation - File size is greater than to configured size for document id:  {id} and file path: {filePath}");
                                            isPreview = false;
                                        }
                                        else
                                        {
                                            _logger.LogDebug($"{indexName}: Start process to convert into pdf document for {indexName} id:  {id}");
                                            isPreview = true;
                                            string extention = Path.GetExtension(filePath).Remove(0, 1);
                                            if (!jobDetail.IsExcelPreview && (extention.ToLower() == "xlsx" || extention.ToLower() == "xls"))
                                            {
                                                _logger.LogDebug($"{indexName}: Start process to extract text from excel for {indexName} id:  {id}");
                                                lstDocumentPage = _extractTextFromFile.GetTextFromExcel(filePath);
                                                _logger.LogDebug($"{indexName}: Completed process to extract text from pdf for {indexName} id:  {id}");


                                            }
                                            else
                                            {
                                                // Convert file to pdf using libre office
                                                string pdfFilePath = ConvertToPDF(jobsDetail, indexName, id, filePath, ref isPreview);
                                                _logger.LogDebug($"{indexName}: Start process to extract text from pdf for {indexName} id:  {id}");
                                                if (jobsDetail.DocumentPreviewSetting.DocumentTextExtactionLibrary.ToLower() == TextExtractionLibrary.IText.ToString().ToLower() &&
                                                    jobsDetail.DocumentPreviewSetting.IText.Enabled)
                                                {
                                                    // Get text from pdf file page wise using iTextSharp
                                                    lstDocumentPage = _extractTextFromFile.GetTextFromPdf(pdfFilePath);
                                                }
                                                else if(jobsDetail.DocumentPreviewSetting.DocumentTextExtactionLibrary.ToLower() == TextExtractionLibrary.Aspose.ToString().ToLower() && 
                                                    jobsDetail.DocumentPreviewSetting.Aspose.Enabled)
                                                {
                                                    // Get text from pdf file page wise using AsposePdf
                                                    lstDocumentPage = _extractTextFromFile.GetTextFromPdfByAspose(pdfFilePath);
                                                }
                                            }
                                            _logger.LogDebug($"{indexName}: Extract text from pdf completed for {indexName} id:  {id}");
                                            //content = string.Join(Environment.NewLine, lstDocumentPage.Select(i => i.Text).ToArray());
                                            var searchResponse = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
                                            {
                                                _source = new { },
                                                query = new
                                                {
                                                    match = new
                                                    {
                                                        id = new
                                                        {
                                                            query = id
                                                        }
                                                    }
                                                }
                                            }));
                                            if (searchResponse.Success)
                                            {
                                                dynamic responseResult = JsonConvert.DeserializeObject<dynamic>(searchResponse.Body).hits;
                                                searchResponse = null;
                                                if (responseResult.hits.Count > 0)
                                                {
                                                    var item = responseResult.hits[0]._source;
                                                    dynamic resultItem = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(item));
                                                    var lstItem = new Dictionary<string, object>();
                                                    lstItem.Add("isProcessed", Constants.PROCESSED);
                                                    lstItem.Add("fileSize", fileSizeKb);
                                                    lstItem.Add("DocumentPage", lstDocumentPage);
                                                    lstItem.Add("isPreview", isPreview);
                                                    // Update pagewise content data into elastic
                                                    var updateResponse = _iElasticConnector._elasticClient.Update<ElasticDocument, object>(new DocumentPath<ElasticDocument>(id), u => u.Index(indexName)
                                                            .Doc(lstItem).DocAsUpsert().Refresh(Refresh.True));
                                                    responseResult = null;
                                                    resultItem = null;
                                                }
                                            }
                                        }
                                        _logger.LogDebug($"{indexName}: Conversion process completed for document id:  {id} and file path : {filePath}");

                                    }
                                    hitsResult = null;
                                    if (lstDocumentPage.Count > 0)
                                        lstDocumentPage.Clear();
                                    if (jobDetail.FileStorageType != StorageProviderType.FileSystem)
                                        if (File.Exists(filePath))
                                            File.Delete(filePath);
                                }
                                catch (Exception ex)
                                {
                                    SetProcessedFailedStatusToDoc(id, indexName);
                                    _logger.LogError($"{indexName}: Initial_Validation - Exception occured while generating if pdf {Environment.NewLine} Exception : {ex.Message} {Environment.NewLine} {ex.StackTrace}");
                                }
                            }
                        }

                        success = hitsResult?.hits?.Count == 0 && failHitsResult?.hits?.Count == 0 ? true : false;
                        //else
                        //    _flag = false;

                        //if (recordProcessedCount >= totalRecord)
                        //    break;
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{indexName}:Initial_Validation - Exception occured during convert into pdf {ex.Message}");
            }
            return true;

        }

        private void SetProcessedFailedStatusToDoc(string id, string indexName)
        {
            var searchId = _iElasticConnector._lowLevelClient.Search<StringResponse>(indexName, PostData.Serializable(new
            {
                _source = new { },
                query = new
                {
                    match = new
                    {
                        id = new
                        {
                            query = id
                        }
                    }
                }
            }));

            if (searchId.Success)
            {
                dynamic response = JsonConvert.DeserializeObject<dynamic>(searchId.Body).hits;
                searchId = null;
                if (response.hits.Count > 0)
                {
                    var item = response.hits[0]._source;
                    Dictionary<string, object> resultItem = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(item));
                    resultItem.FirstOrDefault(x => x.Key == Constants.IS_PROCESSED);
                    object output = "";
                    resultItem.TryGetValue(Constants.IS_PROCESSED, out output);
                    var lstItem = new Dictionary<string, object>();
                    if (output.ToString() == Constants.INCOMPLETE)
                    {
                        lstItem.Add(Constants.IS_PROCESSED, Constants.UNPROCESSED);
                    }
                    else
                    {
                        lstItem.Add(Constants.IS_PROCESSED, Constants.INCOMPLETE);
                    }
                    // Update pagewise content data into elastic
                    var updateResponse = _iElasticConnector._elasticClient.Update<ElasticDocument, object>(new DocumentPath<ElasticDocument>(id), u => u.Index(indexName)
                            .Doc(lstItem).DocAsUpsert().Refresh(Refresh.True));
                    resultItem = null;
                }
            }
        }

        private void GetTextFromHtml(Dictionary<string, object> items, string columns, string IgnoreCharaters)
        {
            string[] columnName = columns?.Split(",", StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < columnName.Length; i++)
            {
                //items = items.ToDictionary(item => item.Key, item =>   );
                if (items.ContainsKey(columnName[i]))
                {
                    items[columnName[i]] = GetPlainTextFromHtml(items[columnName[i]].ToString(), IgnoreCharaters);
                    //items[columnName[i]] = items[columnName[i]].ToString().Split(",", StringSplitOptions.RemoveEmptyEntries);
                }
            }

        }
        private string GetPlainTextFromHtml(string htmlString, string Ignorecharaters)
        {
            try
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlString);
                StringBuilder sb = new StringBuilder();

                foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//text()"))
                {
                    sb.Append(node.InnerText + " ");
                }
                // Added for Removing Junk Data
                string data = System.Web.HttpUtility.HtmlDecode(sb.ToString().Trim());
                htmlString = CleanJunkCharacters(data, Ignorecharaters);
                return htmlString;
            }
            catch
            {
                return htmlString;
            }

        }

        private string CleanJunkCharacters(string data, string IgnoreCharacters)
        {
            try
            {
                StringBuilder sb = new StringBuilder(Regex.Replace(data, @"\s+", " "));
                if (!string.IsNullOrEmpty(IgnoreCharacters))

                {
                    string[] spliltData = IgnoreCharacters.Split('|');
                    foreach (var item in spliltData)
                    {
                        sb.Replace(@item, " ");
                    }
                }
                sb.Replace(@"\u2002", " ");
                sb.Replace(@"\u00A0", " ");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Junk Data Clean-up falied", ex.Message);
                return data;
            }


        }

        private dynamic GetValueFromSource(Dictionary<string, dynamic> source, string fieldName)
        {
            if (source.Keys.Contains(fieldName))
            {
                return source[fieldName];
            }
            return null;
        }
        private string ConvertToPDF(ApplicationConfig jobsDetail, string indexName, string documentId, string sourceFile, ref bool isPreview)
        {
            string destinationPath = Path.Combine(jobsDetail.DocumentPreviewSetting.LibreOfficeSetting.PdfDestinationFilePath, indexName, documentId);
            string extention = Path.GetExtension(sourceFile).Remove(0, 1);
            string filePath = Path.Combine(destinationPath, Path.GetFileNameWithoutExtension(sourceFile) + ".pdf");
            string[] IncludeFileType = jobsDetail.DocumentPreviewSetting.IncludeFileType.Split(",", StringSplitOptions.RemoveEmptyEntries);
            if (!IncludeFileType.Contains(extention))
            {
                isPreview = false;
                return filePath;
            }
            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);
            if (extention.ToUpper() == "PDF")
                File.Copy(sourceFile, filePath, true);
            else
            {
                isPreview = IncludeFileType.Contains(extention) ? true : false;
                if (jobsDetail.DocumentPreviewSetting.Aspose.Enabled && (extention.ToLower() != "xls" || extention.ToLower() != "xlsx"))
                {
                    try
                    {
                        _convertFileToPDF.ConvertToPDFAspose(sourceFile, Path.Combine(destinationPath, Path.GetFileNameWithoutExtension(sourceFile) + ".pdf"), extention);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{indexName}:PDF_Generation - Exception occured during convert into pdf {ex.Message}");
                    }

                }
                else
                {
                    string argument = GetCommandForLibreOffice(jobsDetail.DocumentPreviewSetting.LibreOfficeSetting.Commands, extention);
                    argument = argument.Replace("@sourceFile", $"\"{sourceFile}\"").Replace("@destinationPath", $"\"{destinationPath}\"");
                    // Convert file to pdf using libre office
                    if (!_convertFileToPDF.ConvertToPDF(argument, jobsDetail.DocumentPreviewSetting.LibreOfficeSetting.LibreOfficePath))
                        isPreview = false;
                    if (!isPreview)
                    {
                        _logger.LogError($"{indexName}:[LibreOffice] PDF_Generation - Failed while converting {extention} into pdf. Filepath {sourceFile}");
                    }
                }

            }
            return filePath;
        }

        private string GetCommandForLibreOffice(Dictionary<string, string> commands, string fileExtention)
        {


            return commands.ContainsKey(fileExtention.ToUpper()) ? commands[fileExtention.ToUpper()] : commands.ContainsKey("OTHER") ? commands["OTHER"] : "";

            /*string command = string.Empty;
            if (fileExtention.ToUpper()=="PPT" || fileExtention.ToUpper()=="PPTX")
                command = commands.ContainsKey("PPTX") ? commands["PPTX"] : commands["OTHER"];
            else if (fileExtention.ToUpper() == "DOC" || fileExtention.ToUpper() == "DOCX")
                command = commands.ContainsKey("DOCX") ? commands["DOCX"] : commands["OTHER"]; 
            else if (fileExtention.ToUpper() == "XLS" || fileExtention.ToUpper() == "XLSX")
                command = commands.ContainsKey("XLSX") ? commands["XLSX"] : commands["OTHER"];
            else if (fileExtention.ToUpper() == "OTHER")
                command = commands["OTHER"];
            else
            {
                if (commands.ContainsKey(fileExtention.ToUpper()))
                    command = commands[fileExtention.ToUpper()];
                else
                    command = commands["OTHER"];
            }
            return command;
            */
        }
        private void SplitDynamicFieldValue(IDictionary<string, object> items, IEnumerable<DataRow> configFieldsByClient, string seperator)
        {
            var fields = configFieldsByClient.Where(w => w.Field<int>("ProjectId") == Convert.ToInt32(items["projectId"])
                            && w.Field<bool>("isDynamicField")
                            && w.Field<string>("FieldType").ToLower().Equals("string")).
                        Select(s => new { FieldName = s.Field<string>("FieldName") }).Distinct();
            foreach (var field in fields)
            {
                if (items.ContainsKey(field.FieldName))
                {
                    string result = HttpUtility.HtmlDecode(items[field.FieldName].ToString());
                    var value = Split(result, seperator);
                    if (value.Length > 1)
                        items[field.FieldName] = value;
                    else
                        items[field.FieldName] = Convert.ToString(result).Trim();
                }
            }
        }
        private string[] Split(string value, string seperator)
        {
            //return Regex.Split(value, seperator, RegexOptions.IgnoreCase);
            return string.IsNullOrWhiteSpace(value) ? new string[] { } : value.Split(seperator, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        }
        private void EncryptValue(IDictionary<string, object> items, EncryptionSetting encryptionSetting)
        {
            string[] columnName = encryptionSetting.FieldName?.Split(",", StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < columnName.Length; i++)
            {
                if (items.ContainsKey(columnName[i]))
                {
                    items[columnName[i]] = Encryption.Encrypt(items[columnName[i]].ToString(), encryptionSetting.Key);
                }
            }
        }

        /// <summary>
        /// fied types are required to customize the mapping
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        public Dictionary<string, object> GetFirstObjectForMapping(string jsonData)
        {

            // _logger.LogInformation("Start Bulk Ingest.");
            var item = JsonConvert.DeserializeObject<List<object>>(jsonData)[0];
            var resultItem = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(item));
            return resultItem;
        }

        public Dictionary<string, object> GetProjectIdFromJson(string jsonData)
        {
            var item = JsonConvert.DeserializeObject<List<object>>(jsonData);
            var resultItem = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(item));
            return resultItem?.Select(x => Convert.ToString(x["projectId"]))?.Distinct()?.ToDictionary(t => t, t => new object());
        }

        //private Suggestion[] CreateSuggestions(IDictionary<string, object> items, JobDeatil jobDetail)
        //{
        //    StringBuilder sb = new StringBuilder();
        //    string[] fieldName = jobDetail.SuggestionsField.Split(",", StringSplitOptions.RemoveEmptyEntries);
        //    for (int i = 0; i < fieldName.Length; i++)
        //    {
        //        if (jobDetail.ColumnsToSplit.Contains(fieldName[i]))
        //            sb.Append(string.Join(" ", (string[])items[fieldName[i]]) + " ");
        //        else
        //            sb.Append(items[fieldName[i]] + " ");
        //    }
        //    //  return sb.ToString();
        //    return BuildSuggestions(sb.ToString()).ToArray();
        //}
        public SingleSuggestion[] CreateSuggestions(IDictionary<string, object> items, JobDeatil jobDetail)
        {


            StringBuilder objStringBuilder = new StringBuilder();

            string[] excludeFieldsFromSuggestion = jobDetail.ExcludeFieldsFromSuggestion.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (string key in items.Keys)
            {
                object value;
                items.TryGetValue(key, out value);
                if (excludeFieldsFromSuggestion.Contains(key) || value == null || value.GetType() == typeof(DateTime) || value.GetType() == typeof(int) || key == "id" || key == "attachment"
                    || (value != null && value.GetType().BaseType.Name == "Array"))
                    continue;
                else if (value.GetType() == typeof(string[]))
                    objStringBuilder.Append(string.Join(" ", (string[])value) + " ");
                else
                {
                    string tempValue = Convert.ToString(value);
                    if (!string.IsNullOrEmpty(tempValue) && tempValue.ToLower() != "na")
                        objStringBuilder.Append(tempValue + " ");
                }
            }

            //return sb.ToString();
            return BuildSuggestions(objStringBuilder.ToString(), jobDetail).Distinct().ToArray();
        }
        public IEnumerable<SingleSuggestion> BuildSuggestions(string content, JobDeatil jobDetail)
        {
            try
            {
                content = CleanJunkCharacters(content, jobDetail.IgnoreCharacters);
                //Modify for Removing Junk data
                var regex = new Regex("<[^>]+>", RegexOptions.IgnoreCase);
                content = System.Web.HttpUtility.HtmlDecode((regex.Replace(content, " ")));
                //content = System.Web.HttpUtility.UrlDecode(content);
                content = Regex.Replace(content, @"\t\r\n?|\n|N\A", " ");
                //content = Regex.Replace(content, @"\p{P}", " ");

                var smallAlphanumric = new Regex(@"\b([a-z]+[0-9]+|[0-9]+[a-z]+)[a-z0-9]*\b");// Will not Allowed alpha numeric small capital values
                var onlyAlpha = new Regex(@"(^[a-z]+-?[a-z]+$)|(^\d+$)"); // Will Allowed Only alpha,only Numeric, Alpha with Single hyphen values
                string[] splitContent = content.Split(new char[] { ' ', ',', '!', ';', '_', '/', '?', ':', '"', '(', ')', '[', ']', '.', '*', '@', '^' }, StringSplitOptions.RemoveEmptyEntries);
                return from phrase in splitContent
                       where phrase.Length > 2
                       && phrase.Length <= 20
                       && !phrase.Contains('+')
                       && !phrase.StartsWith('.')
                       && !phrase.StartsWith('-')
                       && !phrase.EndsWith('-')
                       && !phrase.StartsWith("\"")
                       && !phrase.StartsWith("'")
                       && (jobDetail.ExcludeAlphaNumericSuggestion == true ? onlyAlpha.IsMatch(phrase.Trim().ToLowerInvariant()) : !smallAlphanumric.IsMatch(phrase.Trim()))
                       select new SingleSuggestion()
                       {
                           Input = Regex.Replace(phrase.Trim().ToLowerInvariant(), @"\t|\n|\r", " ")

                       };
            }

            catch (Exception ex)
            {
                _logger.LogError("Build Suggestions failed ", ex.Message);
                return new List<SingleSuggestion>();
            }
        }


        private void SplitColumnValue(Dictionary<string, object> items, JobDeatil jobDeatil)
        {
            string[] columnName = jobDeatil.ColumnsToSplit?.Split(",", StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < columnName.Length; i++)
            {
                //items = items.ToDictionary(item => item.Key, item =>   );
                if (items.ContainsKey(columnName[i]))
                {
                    items[columnName[i]] = Split(items[columnName[i]].ToString(), jobDeatil.SplitSeperator);
                }
            }
        }
        private Stream GetCloudFileStreamFromTenantSourceStorage(string fileName, string sourceId, IMongoConfigFactory mongoConfigFactory, JobDeatil jobDeatil, FileFromCloudStorage fileFromCloud, string clientId)
        {
            var featureConfiguration = mongoConfigFactory.GetByFieldValue("featureType", "FileIngestion");
            Stream fileStream = null;
            try
            {
                if (featureConfiguration != null)
                {
                    var previewJobConfig = BsonSerializer.Deserialize<FeatureConfiguration>(featureConfiguration);
                    var indexTypes = previewJobConfig?.indexTypes;
                    if (indexTypes != null)
                    {
                        foreach (var indexType in indexTypes)
                        {
                            if (indexType != null && indexType.indexId.Equals(sourceId))
                            {
                                if (indexType.useTenantSourceStorage)
                                {
                                    string sourceType = !string.IsNullOrEmpty(indexType.sourceType) &&
                                    indexType.sourceType.ToLower() == "network" ? StorageTypes.FileSystem.ToString() : indexType.sourceType;
                                    StorageTypes spt = (StorageTypes)Enum.Parse(typeof(StorageTypes), sourceType ?? indexType.sourceType, true);
                                    StorageProvider storageProvider = new StorageProvider(spt);
                                    var storageService = storageProvider.StorageService(indexType.sourceUrl);
                                    fileStream = storageService.GetFileStream(fileName);
                                }
                                else
                                {
                                    fileStream = fileFromCloud.GetStreamFromCloud(Path.Combine(clientId, fileName), jobDeatil.FileStorageType);
                                }
                                return fileStream;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to fetch file from cloud tenant source storage sourceId:  {sourceId} and  fileName : {fileName} errormessage: {ex.Message}");

            }

            return fileStream;
        }
    }


    public interface IEntity
    {
        string id { get; set; }
    }
    public class ElasticDocument : IEntity
    {
        public string id { get; set; }
        public string Content { get; set; }
        public Attachment Attachment { get; set; }
        public DocumentPage[] DocumentPage { get; set; }
        public string IsProcessed { get; set; }
        [Completion(Name = "suggestions", SearchAnalyzer = "standard", Analyzer = "standard", PreserveSeparators = true)]
        public SingleSuggestion[] Suggestions { get; set; }

    }
    public class NER_Entities
    {
        public string[] Location { get; set; }
        public string[] Person { get; set; }
        public string[] Organization { get; set; }
        public string[] Percentage { get; set; }
        public string[] Date { get; set; }
        public string[] Cardinal { get; set; }
    }

    public class DataDictionary_Elastic : IEntity
    {

        public string key { get; set; }
        public int projectid { get; set; }
        public string type { get; set; }
        public DataDictionary_Attributes attributes { get; set; }
        public string id { get; set; }
    }
    public class DataDictionary_Attributes
    {
        public int frequency { get; set; }
        public float probabilities { get; set; }
        public List<string> values { get; set; }
    }

    public class AutoSuggestion_Entities
    {

        //public Dictionary<string, dynamic> Suggestions { get; set; }

        public dynamic SuggestionData { get; set; }

    }

    public class AutoSuggestion_Elastic
    {
        public string Recorded_id { get; set; }
    }

}
