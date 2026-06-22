using Microsoft.Extensions.Logging;
using MongoDB.Bson.IO;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Connector;
using SearchLego.DataSeeder.Elastic;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SearchLego.DataSeerer.Integration
{
    public class IngestData
    {
        private readonly ILogger<dynamic> _logger;
        private readonly ApplicationConfig _jobsDetail;
        private readonly IDataSourceFactory _iDataSourceFactory;
        private readonly IMongoConfigFactory _iMongoConfigFactory;
        private readonly IElasticIndexBuilder _iElasticIndexBuilder;
        private readonly IElasticIngest _iElasticIngest;
        private readonly ISLBConfig _iSLBConfig;
        private readonly ISettingValue _iSettingValue;
        private readonly IUtilityFunctions _iUtilityFunctions;
        private readonly IClientCrawlSetting _clientCrawlSetting;
        private readonly IMongoConfigFactory _mongoClientCrawlFactory;
        private const string format = "dd MMMM yyyy HH:mm:ss.fff";
        private readonly IMongoConfigFactory _iMongoClientConfig;
        private readonly IMongoConfigFactory _iMongoConfigurationFactory;
        private readonly ITenantService _iTenantService;

        public IngestData(ILogger<dynamic> logger, ApplicationConfig jobsDetail, IElasticIndexBuilder iElasticIndexBuilder,
            IElasticIngest iElasticIngest, IDataSourceFactory iDataSourceFactory, IMongoConfigFactory iMongoConfigFactory,
            ISLBConfig iSLBConfig, ISettingValue iSettingValue, IUtilityFunctions iUtilityFunctions,
            IMongoConfigFactory mongoClientCrawlFactory, IClientCrawlSetting clientCrawlSetting, IMongoConfigFactory iMongoClientConfig,
            ITenantService iTenantService, IMongoConfigFactory iMongoConfigurationFactory)

        {
            _logger = logger;
            _jobsDetail = jobsDetail;
            _iElasticIndexBuilder = iElasticIndexBuilder;
            _iElasticIngest = iElasticIngest;
            _iDataSourceFactory = iDataSourceFactory;
            _iMongoConfigFactory = iMongoConfigFactory;
            _iSLBConfig = iSLBConfig;
            _iSettingValue = iSettingValue;
            _iUtilityFunctions = iUtilityFunctions;
            _clientCrawlSetting = clientCrawlSetting;
            _mongoClientCrawlFactory = mongoClientCrawlFactory;
            _iMongoClientConfig = iMongoClientConfig;
            _iTenantService = iTenantService;
            _iMongoConfigurationFactory = iMongoConfigurationFactory;
        }
        public void Ingest(string jobId)
        {
            bool ingestStatus = false;
            var job = _jobsDetail.JobDeatil.Where(i => i.Enabled && i.Id.Equals(jobId)).FirstOrDefault();
            _logger.LogInformation($"Execution started for index type ({job?.IndexType})");

            List<string> lstClientIds = null;
            DataSet objDSConfig = null;
            IEnumerable<DataRow> objConfig = null;
            IEnumerable<DataRow> configFieldsByClient = null;
            IList<Tenant> tenantList = null;
            Tenant currentTenant = null;
            if (_jobsDetail.EnableUISetting)
            {
                objDSConfig = _iDataSourceFactory.GetDataSet(_jobsDetail.SearchUISetting.SQLQuery.Replace("@indexName", "'" + job.IndexType + "', ").Replace(_jobsDetail.SearchUISetting.ProcParameterName, "'" +  /*settingValue.LastConfigUpdated*/ ' ' + "'"));
                if (objDSConfig != null && objDSConfig.Tables.Count > 0 && objDSConfig.Tables[0].Rows.Count > 0)
                {
                    var lstConfig = objConfig = objDSConfig.Tables[0].AsEnumerable();
                    lstClientIds = lstConfig.Select(i => Convert.ToString(i.Field<int>("AccountId"))).Distinct().ToList<string>();
                }
            }
            else
            {
                //  Adding client zero for non IF or not use to client/project
                lstClientIds = new List<string>();
                lstClientIds.Add("0");
            }
            //Re-Assiging value to  lstClientIds OR TenentIds
            if (_jobsDetail.TenantType.Equals(TenantConnectionType.Multi.ToString()))
            {
                TenantData tenantData = new TenantData(_logger, _iTenantService);

                tenantList = tenantData.TenantDataPrepare(jobId,TenantConnectionType.Multi, _jobsDetail);
                lstClientIds = tenantList.Where(i => i.ModuleMapped.Any(m => m.Name == job.IndexType)).Select(s => s.TenantId).ToList();
            }
            if (lstClientIds == null || lstClientIds.Count == 0)
            {
                _logger.LogError($"Initial_Validation - job id : {jobId} Job type: {job.IndexType} , Meta data not found in sql proc. ");
                return;
            }

            _clientCrawlSetting.BuildClientWiseSetting(lstClientIds, job, _mongoClientCrawlFactory, _iUtilityFunctions, tenantList);
            if (job != null)
            {
                var tabClientList = job.TabClientList?.Split(",");
                var allowedClientList = _jobsDetail.AllowedClientList?.Split(",");
                foreach (string clientId in lstClientIds)
                {
                    try
                    {
                        if (!allowedClientList.Contains(clientId.ToString()))
                            continue;

                        if (tabClientList != null && tabClientList.Length != 0)
                        {
                            if (!tabClientList.Contains(clientId.ToString()))
                                continue;
                        }

                        if (_jobsDetail.TenantType.Equals(TenantConnectionType.Multi.ToString()))
                            currentTenant = tenantList?.Where(t => t.TenantId == clientId).First();

                        // get last modified date according to job.
                        string indexName = _iUtilityFunctions.GetIndexName(job.IndexType, clientId, _iUtilityFunctions.GetIndexPrefix(job, currentTenant));
                        CrawlSetting objCrawlSetting = _clientCrawlSetting.GetSettingById(_mongoClientCrawlFactory, indexName);

                        // Geting client config 
                        var bsonClientConfig = _iMongoClientConfig.GetById(clientId.ToString());
                        ClientConfiguration objClientConfig = bsonClientConfig?.ConvertBsonToObject<ClientConfiguration>();

                        // var settingValue = _iSettingValue.ReadParameterValue(job.Id, _jobsDetail.ParameterFileName);
                        bool isForcedToFullCrawl = objCrawlSetting.IsForcedToFullCrawl;
                        objCrawlSetting.LastUpdatedDate = isForcedToFullCrawl ? "" : objCrawlSetting.LastUpdatedDate;
                        if (_jobsDetail.EnableUISetting)
                        {
                            bool skipIngestData = false;
                            configFieldsByClient = objConfig.Where(i => i.Field<int>("AccountId").ToString() == clientId).Select(s => s);

                            // _iSLBConfig.UpdateSBLTabName(_logger, _iMongoConfigFactory, configFieldsByClient, indexName);

                            //string FullCrawlExecutionTime = objCrawlSetting.FullCrawlExecutionTime;
                            objCrawlSetting.LastConfigUpdated = (isForcedToFullCrawl && !objCrawlSetting.NoUpdateInConfig) ? "" : objCrawlSetting.LastConfigUpdated;
                            objDSConfig = _iDataSourceFactory.GetDataSet(_jobsDetail.SearchUISetting.SQLQuery.Replace("@indexName", "'" + job.IndexType + "', ").Replace(_jobsDetail.SearchUISetting.ProcParameterName, "'" + objCrawlSetting.LastConfigUpdated + "'"));
                            if (objDSConfig != null && objDSConfig.Tables.Count > 0 && objDSConfig.Tables[0].Rows.Count > 0)
                            {
                                if (objDSConfig.Tables[0].AsEnumerable().Any(i => i.Field<int>("AccountId").ToString().Equals(clientId)))
                                    if (!isForcedToFullCrawl)
                                    {
                                        if (string.IsNullOrEmpty(objCrawlSetting.LastConfigUpdated))
                                            isForcedToFullCrawl = true;
                                        else
                                        {
                                            IsFullCrawlExecutionRequired(objCrawlSetting, ref isForcedToFullCrawl, ref skipIngestData);
                                        }
                                    }
                            }
                            if (skipIngestData)
                            {
                                _logger.LogInformation($"Config has been modified. Please wait for full crawl to run for index '{indexName}' or you can force to run full crawl by change setting value ");
                                continue; // Task.CompletedTask;
                            }
                        }
                        else
                        {
                            if (!isForcedToFullCrawl && objCrawlSetting.IsScheduledToFullCrawl)
                            {
                                bool skipIngestData = false;
                                IsFullCrawlExecutionRequired(objCrawlSetting, ref isForcedToFullCrawl, ref skipIngestData);
                                if (skipIngestData)
                                {
                                    _logger.LogInformation($"Schedule crawl has been modified. Please wait for full crawl to run for index '{indexName}' or you can force to run full crawl by change setting value ");
                                    continue;
                                }
                                // objCrawlSetting.LastConfigUpdated = DateTime.Now.ToString(format);
                                objCrawlSetting.IsForcedToFullCrawl = false;
                                objCrawlSetting.IsScheduledToFullCrawl = false;
                                _clientCrawlSetting.UpdateSetting(_mongoClientCrawlFactory, objCrawlSetting);
                            }
                        }
                        objCrawlSetting.LastUpdatedDate = isForcedToFullCrawl ? "" : objCrawlSetting.LastUpdatedDate;
                        if (isForcedToFullCrawl && _jobsDetail.EnableUISetting && !objCrawlSetting.NoUpdateInConfig)
                        {
                            _iSLBConfig.UpdateSBLConfig(clientId, _logger, _iMongoConfigFactory, _mongoClientCrawlFactory, _clientCrawlSetting,
                                objCrawlSetting, objDSConfig, job, format, _iUtilityFunctions, currentTenant);
                            
                            _logger.LogInformation($"SLBConfig has updated for indextype {job.IndexType} , clientId: {clientId} and IsForcedToFullCrawl: {isForcedToFullCrawl}, NoUpdateInConfig: {objCrawlSetting.NoUpdateInConfig}");
                        }
                        else
                        {
                            _logger.LogInformation($"SLBConfig has not updated for indextype {job.IndexType} , clientId: {clientId} and  IsForcedToFullCrawl: {isForcedToFullCrawl}, NoUpdateInConfig: {objCrawlSetting.NoUpdateInConfig}");
                        }
                        _logger.LogDebug($"Data in elasticsearch was last modified on {objCrawlSetting.LastUpdatedDate} in indextype '{job.IndexType}' by job id {jobId} ");
                        _logger.LogDebug($"Started geting SQL data for indextype '{job.IndexType}'");

                        try
                        {
                            ingestStatus = IngestDataByClientId(jobId, ingestStatus, job, objCrawlSetting, isForcedToFullCrawl, clientId, _jobsDetail, configFieldsByClient, objClientConfig, currentTenant);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Initial_Validation - job id : {jobId} clientId: {clientId} IndexType {job.IndexType}, ingest exception, {ex.Message}, {ex.StackTrace}");
                        }
                        ///create index and ingest data by client

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Initial_Validation - job id : {jobId} clientId: {clientId} IndexType {job.IndexType}, ingest exception in loop, {ex.Message}, {ex.StackTrace}");
                    }



                }
            }
            else
            {
                _logger.LogInformation($"Config is missing for indextype {job.IndexType}'");
            }
        }

        private void IsFullCrawlExecutionRequired(CrawlSetting objCrawlSetting, ref bool isForcedToFullCrawl, ref bool skipIngestData)
        {
            var date = Convert.ToDateTime(objCrawlSetting.FullCrawlExecutionTime);
            var timeToRunFullCrawl = DateTime.Today.AddHours(date.Hour).AddMinutes(date.Minute);
            if (DateTime.Now > timeToRunFullCrawl && !objCrawlSetting.DisableAutomatedFullCrawl)
                isForcedToFullCrawl = true;
            else
                skipIngestData = true;
        }

        private bool IngestDataByClientId(string jobId, bool ingestStatus, JobDeatil job, CrawlSetting objCrawlSetting,
            bool isForcedToFullCrawl, string clientId, ApplicationConfig jobsDetail,
            IEnumerable<DataRow> configFieldsByClient, ClientConfiguration clientConfig, Tenant currentTenant)
        {
            string indexNameForClient = _iUtilityFunctions.GetIndexName(job.IndexType, clientId, _iUtilityFunctions.GetIndexPrefix(job, currentTenant));
            string json = string.Empty;
            int updatedRecords = 0;
            var lastUpdatedDate = objCrawlSetting.LastUpdatedDate == null || objCrawlSetting.LastUpdatedDate == "" ?
                "" : Convert.ToDateTime(objCrawlSetting.LastUpdatedDate).AddSeconds(-1).ToString(format);
            var currentTime = DateTime.Now.ToString(format);

            _logger.LogInformation($"Full Crawl is " + (isForcedToFullCrawl ? "required" : "not required") + $" for index type '{job.IndexType}' and index {indexNameForClient}");
            //#if DEBUG

            if (_jobsDetail.TenantType == TenantConnectionType.Multi.ToString())
            {
                json = _iDataSourceFactory.GetData((job.SQLQuery.Replace(job.ProcParameter_LastUpdated, "'" + lastUpdatedDate + "'") + " , '" + clientId + "'"), _jobsDetail.DefaultValue, _iUtilityFunctions.GetConnectionString(jobsDetail, currentTenant));
            }
            else
            {
                json = _iDataSourceFactory.GetData((job.SQLQuery.Replace(job.ProcParameter_LastUpdated, "'" + lastUpdatedDate + "'") + " , '" + clientId + "'"), _jobsDetail.DefaultValue);
            }
            // _logger.LogInformation($"SQL Date for job id {jobId}  is this : { json} ");
            if (!string.IsNullOrWhiteSpace(json))
            {
                job.IngestBatchSize = job.IngestBatchSize == 0 ? 10000 : job.IngestBatchSize;
                if (isForcedToFullCrawl)
                    _logger.LogInformation($"Started building index type '{job.IndexType}' and index '{indexNameForClient}'");
                // if last modified date is blank, delete and recreate index 
                // if (_jobsDetail.EnableUISetting)
                //  _iElasticIndexBuilder.BuildIndexByClient<ElasticDocument>(job?.IndexType, clientId, isForcedToFullCrawl, _jobsDetail.EnabledSynonyms, firstRecord);
                //else

                BuildIndexes(indexNameForClient,
                    isForcedToFullCrawl,
                    json,
                    objCrawlSetting.IsAutoSuggestionExecutionRequired,
                    job.EnableSingleWordSuggestion,
                    job.SuggestionsGramLimit
                    );

                _logger.LogDebug($"Udating the found records in ElasticSearch for job id : {jobId} , indextype '{job.IndexType}'  and index '{indexNameForClient}'");
                if (job.IsAttachment) //  data Ingest with attachment and without attachment
                {
                    job.PipelineName = job.PipelineName.IndexOf("_") > 0 ? job.PipelineName.Substring(0, job.PipelineName.IndexOf("_")) : job.PipelineName;
                    job.PipelineName = $"{job.PipelineName}_{clientId}";
                    ingestStatus = _iElasticIngest.BulkUpdateWithAttachment(json, job, indexNameForClient, jobsDetail, configFieldsByClient, clientConfig, _iMongoConfigurationFactory, out updatedRecords);
                }
                else
                    ingestStatus = _iElasticIngest.BulkUpdateDocument(json, job, indexNameForClient, configFieldsByClient, clientConfig, out updatedRecords);

                if (ingestStatus)
                {
                    objCrawlSetting.LastUpdatedDate = currentTime;
                    objCrawlSetting.IsForcedToFullCrawl = false;
                    objCrawlSetting.NoUpdateInConfig = true;
                    _iUtilityFunctions.BuildCrawlHistory(objCrawlSetting, isForcedToFullCrawl, objCrawlSetting.LastUpdatedDate, updatedRecords);
                    _clientCrawlSetting.UpdateSetting(_mongoClientCrawlFactory, objCrawlSetting);
                    // update last modified date according to job.
                    /// _iSettingValue.WriteParameterValue(job.Id, _jobsDetail.ParameterFileName, j => j.LastUpdatedDate = DateTime.Now.ToString(formate));
                    _logger.LogInformation($"Elastic Ingest has been done successfully for job id : {jobId}   {DateTime.Now.ToString()} index type '{job.IndexType}' and index '{indexNameForClient}'");
                }
                else
                    _logger.LogInformation($"Elastic Ingest has been failed for job id : {jobId}  index type '{job.IndexType}'  and index '{indexNameForClient}'");
            }
            else
            {
                objCrawlSetting.LastUpdatedDate = currentTime;
                // update last modified date according to job.
                _clientCrawlSetting.UpdateSetting(_mongoClientCrawlFactory, objCrawlSetting);
                _logger.LogInformation($"There is no updated record found in source/database for updates in indextype '{job.IndexType}', index '{indexNameForClient}', job id : {jobId} , Time:   {DateTime.Now.ToString()} ");
            }
            return ingestStatus;
        }

        private void BuildIndexes(string indexName, bool isForcedToFullCrawl, string json, bool IsAutoSuggestionExecutionRequired, bool EnableSinglewordSuggestion, int suggestionsGramLimit)
        {
            var data = _iElasticIngest.GetFirstObjectForMapping(json);
            _iElasticIndexBuilder.BuildIndex<ElasticDocument>(indexName, isForcedToFullCrawl, data);
            if (IsAutoSuggestionExecutionRequired && !EnableSinglewordSuggestion)
            {
                var projectIdDict = _iElasticIngest.GetProjectIdFromJson(json);
                _iElasticIndexBuilder.BuildIndex<AutoSuggestion_Elastic>(indexName + Constants.AUTO_COMPLETE, isForcedToFullCrawl, projectIdDict, suggestionsGramLimit > 0 ? suggestionsGramLimit : 5);
            }
        }
    }
}
