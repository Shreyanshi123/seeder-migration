using DocumentFormat.OpenXml.Drawing;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using BsonIO = MongoDB.Bson.IO;

namespace SearchLego.DataSeerer.Integration
{
    public class SLBConfig : ISLBConfig
    {
        public void ExecuteSlbConfig(string clientId, ILogger<dynamic> logger, IMongoConfigFactory iMongoConfigFactory, ISettingValue iSettingValue, DataSet objConfig, JobDeatil jobDeatil, string fileName, string format, IUtilityFunctions iUtilityFunctions)
        {


        }

        public void UpdateSBLConfig(string clientId, ILogger<dynamic> logger, IMongoConfigFactory iMongoConfigFactory,
                IMongoConfigFactory mongoClientCrawlFactory, IClientCrawlSetting clientCrawlSetting,
                CrawlSetting crawlSetting, DataSet objDSConfig, JobDeatil jobDeatil, string format, IUtilityFunctions iUtilityFunctions,Tenant currentTenant)
        {
            lock (this)
            {
                logger.LogInformation($"Config update for clientId {clientId} started for job id : {jobDeatil.Id} index '{jobDeatil.IndexType}'");
                try
                {
                    // string mongoClientId = iUtilityFunctions.GetIndexName(jobDeatil.IndexType, clientId);

                    bool isExisingClient = false;
                    dynamic objClient = iMongoConfigFactory.GetById(clientId);
                    if (objClient != null)
                    {    // Get existing configuration 
                        objClient = JValue.Parse(ToJson(objClient));
                        isExisingClient = true;
                    }
                    if (objDSConfig != null && objDSConfig.Tables.Count > 0)
                    {
                        if (objDSConfig.Tables[0].Rows.Count > 0)
                        {
                            var configFieldValue = objDSConfig.Tables[0].AsEnumerable();
                            // Find list of clients to be update
                            var clientList = configFieldValue.GroupBy(g => g.Field<int>("AccountId")).Select(s => s.Key);
                            // get default config from default template.
                            dynamic defaultClientConfig = GetConfigForClient();
                            objClient = isExisingClient ? objClient : defaultClientConfig;

                            //return default template for client if not there in the system
                            dynamic objClientConfig = objClient == null ? JValue.Parse(JsonConvert.SerializeObject(defaultClientConfig)) : objClient;

                            objClientConfig = AddAttributesIfNotExist(objClientConfig, defaultClientConfig);
                            //get total projects from the current client
                            var totalProjects = configFieldValue.Where(i => i.Field<int>("AccountId").Equals(Convert.ToInt32(clientId)))
                                .GroupBy(g => g.Field<int>("ProjectId")).Select(s => s.Key);

                            List<dynamic> lstProjects = isExisingClient ? JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(objClientConfig.projects)) : new List<dynamic>();

                            //iterate over all project 
                            foreach (var projectId in totalProjects)
                                AddUpdateProjectToClient(clientId, iUtilityFunctions, objClient, configFieldValue, defaultClientConfig, objClientConfig, lstProjects, projectId, jobDeatil, currentTenant);

                            objClientConfig.projects = JArray.Parse(JsonConvert.SerializeObject(lstProjects));
                            objClientConfig.clientId = clientId;

                            if (!isExisingClient)
                                objClientConfig._id = clientId;
                            //UpdateConfig(iMongoConfigFactory, mongoClientCrawlFactory, clientCrawlSetting, crawlSetting, format, objClient);
                            string json = JsonConvert.SerializeObject(objClientConfig);
                            var objClientConfigBson = BsonSerializer.Deserialize<BsonDocument>(json);
                            iMongoConfigFactory.Update(objClientConfigBson);

                            //update crawler setting

                            crawlSetting.LastConfigUpdated = DateTime.Now.ToString(format);
                            crawlSetting.IsForcedToFullCrawl = false;
                            clientCrawlSetting.UpdateSetting(mongoClientCrawlFactory, crawlSetting);
                            logger.LogInformation($"Config has been updated successfully for clientId {clientId} for job id : {jobDeatil.Id} index '{jobDeatil.IndexType}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"Exception occurred while inserting configuration into mongoDB for job id : {jobDeatil.Id} index '{jobDeatil.IndexType}' Error Message:  { ex.Message} ");
                    throw ex;
                }
            }


        }

        public void UpdateSBLTabName(ILogger<dynamic> logger, IMongoConfigFactory iMongoConfigFactory, IEnumerable<DataRow> configFieldsByClient, string indexType)
        {
            try
            {
                string clientId = configFieldsByClient != null && configFieldsByClient.ToArray().Length > 0 ? configFieldsByClient.ToArray()[0].Field<int>("AccountId").ToString() : "0";
                dynamic objClient = iMongoConfigFactory.GetById(clientId);
                if (objClient == null)
                    return;
                objClient = JValue.Parse(ToJson(objClient));
                bool isUpdateRequired = false;
                var projects = configFieldsByClient.GroupBy(g => g.Field<int>("ProjectId")).Select(s => s.Key);
                foreach (var item in projects)
                {
                    dynamic foundProject = findProject(objClient.projects, Convert.ToString(item));
                    if (foundProject == null)
                        continue;
                    var totalTabs = configFieldsByClient.Where(i => i.Field<int>("ProjectId").Equals(item))
                        .GroupBy(g => g.Field<int>("PageId")).Select(s => s);
                    foreach (var tab in totalTabs)
                    {
                        dynamic foundTab = findTab(foundProject, tab.Key, "");
                        if (foundTab == null)
                            continue;
                        var tabList = tab.Select(i => i.Field<string>("PageNameToShow")).Distinct().ToArray();
                        if (foundTab.config.indexName == indexType)
                        {
                            string tabName = tabList.Length > 0 ? tabList[0] : "";
                            if (foundTab.Tab != tabName)
                            {
                                foundTab.Tab = tabName;
                                isUpdateRequired = true;
                            }
                        }
                    }
                }
                if (isUpdateRequired)
                {
                    string json = JsonConvert.SerializeObject(objClient);
                    var objClientConfigBson = BsonSerializer.Deserialize<BsonDocument>(json);
                    iMongoConfigFactory.Update(objClientConfigBson);

                }

            }
            catch (Exception ex)
            {
                logger.LogError($"Exception occurred while updating Tab name into mongoDB for index '{indexType}' Error Message:  { ex.Message} ");
            }


        }
        /// <summary>
        /// Add project to client
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="iUtilityFunctions"></param>
        /// <param name="objClient"></param>
        /// <param name="configFieldValue"></param>
        /// <param name="defaultClientConfig"></param>
        /// <param name="objClientConfig"></param>
        /// <param name="lstProjects"></param>
        /// <param name="projectId"></param>

        private void AddUpdateProjectToClient(string clientId, IUtilityFunctions iUtilityFunctions, dynamic objClient,
            EnumerableRowCollection<DataRow> configFieldValue, dynamic defaultClientConfig, dynamic objClientConfig,
            List<dynamic> lstProjects, int projectId, JobDeatil jobDeatil, Tenant currentTenant)
        {
            var tabId = 0;
            //find proejct if exists or return .
            dynamic existingProject = findProject(objClientConfig.projects, Convert.ToString(projectId));
            //return default template if does not exists in mongodb  || existingProject.tabs.Count ==0


            dynamic objProjectConfig = existingProject == null ? JValue.Parse(JsonConvert.SerializeObject(defaultClientConfig.projects[0])) : existingProject;
            objProjectConfig = AddAttributesIfNotExist(objProjectConfig, defaultClientConfig.projects[0]);

            var totalTabs = from f in configFieldValue
                            where f.Field<int>("AccountId").Equals(Convert.ToInt32(clientId)) && f.Field<int>("ProjectId").Equals(projectId)
                            group f by new { page = f.Field<int>("PageId"), VirtualTabId = f.Field<int?>("VirtualTabId") } into g
                            select g;


            List<dynamic> lstTabsConfig = (objClient == null || existingProject == null) ? new List<dynamic>() : JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(objProjectConfig.tabs));
            lstTabsConfig = lstTabsConfig == null ? new List<dynamic>() : lstTabsConfig;

            //update tabs setting for project
            foreach (var item in totalTabs)
            {
                AddUpdateTabSetting(objProjectConfig, lstTabsConfig, item, iUtilityFunctions, Convert.ToString(clientId), jobDeatil, defaultClientConfig, tabId, currentTenant);
            }

            objProjectConfig.projectId = projectId.ToString();
            objProjectConfig.tabs = JArray.Parse(JsonConvert.SerializeObject(lstTabsConfig));
            foreach (var tab in objProjectConfig.tabs)
            {
                tab.TabId = ++tabId;
            }

            //maintain the tabs at original index of already created 
            if (existingProject != null)
            {
                int count = 0;
                dynamic existingProjects = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(lstProjects));
                foreach (var lstItem in existingProjects)
                {
                    if (lstItem.projectId == existingProject.projectId)
                    {
                        lstProjects[count] = objProjectConfig;
                        break;
                    }
                    count++;
                }
            }
            else
                lstProjects.Add(objProjectConfig);
        }


        private dynamic findClient(dynamic clients, string id)
        {
            dynamic foundClient = null;
            foreach (dynamic item in clients)
            {
                if (item.clientId == id)
                {
                    foundClient = item;
                    break;
                }
            }

            return foundClient;
        }
        private dynamic findProject(dynamic projects, string id)
        {
            dynamic objProject = null;
            foreach (dynamic item in projects)
            {
                if (item.projectId == id)
                {
                    objProject = item;
                    break;
                }
            }
            return objProject;
        }

        private dynamic AddAttributesIfNotExist(dynamic existingSource, dynamic defaultSource)
        {
            dynamic obj = null;
            bool found = false;
            try
            {
                obj = JObject.Parse(JsonConvert.SerializeObject(existingSource));
                foreach (var dItem in defaultSource)
                {
                    found = false;
                    foreach (var eItem in existingSource)
                    {
                        if (dItem.Name == eItem.Name)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        obj.Add(dItem);
                }
            }
            catch
            {
                obj = existingSource;
            }
            return obj == null ? existingSource : obj;
        }

        private dynamic findTab(dynamic project, int id, string tab, bool isMultipage = false, IEnumerable<Guid?> pageRefIds = null)
        {
            dynamic foundTab = null;
            var isMulipageTypeTab = pageRefIds != null && pageRefIds.Count() > 0;
            foreach (dynamic item in project.tabs)
            {
                if (isMultipage || isMulipageTypeTab)
                {
                    //if (item.PageId == id && item.Tab == tab)
                    var isPageRefIdMatched = pageRefIds.Where(x => x != null && x.ToString() == Convert.ToString(item.pageReferenceId)).Count() > 0;
                    if (item.PageId == id && isPageRefIdMatched)
                    {
                        foundTab = item;
                        break;
                    }
                }
                else
                    if (item.PageId == id)
                {
                    foundTab = item;
                    break;
                }
            }
            return foundTab;
        }

        private dynamic findVirtualConfig(dynamic virtualPage, string pageRefId)
        {
            dynamic foundTab = null;
            foreach (dynamic item in virtualPage)
            {
                if (item.pageReferenceId == pageRefId)
                {
                    foundTab = item;
                    break;
                }
            }
            return foundTab;
        }

        /// <summary>
        /// search search ui config based on indextype and clientId
        /// </summary>
        /// <param name="objProjectConfig"></param>
        /// <param name="lstTabsConfig"></param>
        /// <param name="item"></param>
        /// <param name="iUtilityFunctions"></param>
        /// <param name="clientId"></param>
        private void AddUpdateTabSetting(dynamic objProjectConfig, List<dynamic> lstTabsConfig, IGrouping<object, DataRow> item,
            IUtilityFunctions iUtilityFunctions, string clientId, JobDeatil jobDeatil, dynamic defaultClientConfig, dynamic tabId, Tenant currentTenant)
        {
            string indexType = "";
            string tabName = "";
            List<dynamic> listOfFields = new List<dynamic>();
            List<dynamic> listOfFieldsForSearch = new List<dynamic>();
            List<dynamic> lstFacetGroupStrFields = new List<dynamic>();
            List<dynamic> lstFacetGroupDatesFields = new List<dynamic>();
            List<dynamic> listOfFacets = new List<dynamic>();
            List<dynamic> listOfSortFields = new List<dynamic>();
            List<dynamic> listOfVirtualPage = new List<dynamic>();
            SetDefaultValue(jobDeatil, listOfFields, listOfFieldsForSearch, listOfSortFields);
            var pageGroup = item.ToArray().GroupBy(g => g.Field<Guid>("PageRefId")).Select(s => s);
            bool isMultiPage = false;
            dynamic multiPage = null;
            // checking is is multi page 
            if (pageGroup.Count() > 1)
            {
                multiPage = JsonConvert.DeserializeObject<dynamic>(JsonConvert.SerializeObject(defaultClientConfig.projects[0].tabs[0].multiPageSetting));
                isMultiPage = true;
            }
            var commonFields = isMultiPage ? pageGroup.SelectMany(s => s).Where(w => w.Field<bool>("isCommon")) : pageGroup.SelectMany(s => s);
            int pageId = 0;
            var _commonField = commonFields.ToArray().Length > 0 ? commonFields.ToArray()[0] : null;
            pageId = _commonField != null ? _commonField.Field<int>("PageId") : 0;
            var tab = _commonField != null ? _commonField.Field<string>("VirtualTabName").ToString() : "";

            var PageRefId = _commonField != null ? _commonField.Field<Guid?>("PageRefId") : null;
            var _allPageRefIds = pageGroup.SelectMany(s => s).Select(x =>
            {
                var r = x.Field<Guid?>("PageRefId");
                return r != null ? r : null;
            }).Where(x => x != null && x != Guid.Empty).Distinct();
            dynamic existingTab = findTab(objProjectConfig, pageId, tab, isMultiPage, _allPageRefIds);
            dynamic objtabConfig = existingTab == null ? defaultClientConfig.projects[0].tabs[0] : existingTab;
            objtabConfig = AddAttributesIfNotExist(objtabConfig, defaultClientConfig.projects[0].tabs[0]);

            //objtabConfig =JsonConvert.DeserializeObject(JsonConvert.SerializeObject(objtabConfig));
            // creating common record in config.
            if (commonFields.Any())
            {
                foreach (var commonItem in commonFields)
                {
                    AddUpdateFieldsInTab(out indexType, out tabName, listOfFields, listOfFieldsForSearch, lstFacetGroupStrFields, lstFacetGroupDatesFields,
                        listOfFacets, listOfSortFields, commonItem);
                }

                var config = GetConfig(iUtilityFunctions, objProjectConfig, defaultClientConfig, jobDeatil,
                         listOfFields, listOfFieldsForSearch, lstFacetGroupStrFields, lstFacetGroupDatesFields, listOfFacets, listOfSortFields, objtabConfig?.config);

                objtabConfig.config = config;
                objtabConfig.config.indexName = iUtilityFunctions.GetIndexName(jobDeatil.IndexType, clientId, iUtilityFunctions.GetIndexPrefix(jobDeatil, currentTenant));
                objtabConfig.config.displayTemplateName = existingTab == null ? jobDeatil.IndexType : objtabConfig.config.displayTemplateName;
                objtabConfig.Tab = existingTab == null ? tabName : objtabConfig.Tab;
                //var PageRefId = commonFields.ToArray().Length > 0 ? commonFields.ToArray()[0].Field<Guid?>("PageRefId") : null;
                objtabConfig.pageReferenceId = PageRefId == null ? null : PageRefId.ToString();
            }
            //We are preparing list of Virtual Page 
            string defaultPageRefId = string.Empty;
            if (isMultiPage)
            {
                dynamic virtualPage = (existingTab == null || existingTab.multiPageSetting == null) ? JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(multiPage.virtualPages)) : existingTab.multiPageSetting.virtualPages;
                foreach (var pageItem in pageGroup)
                {
                    ClearConfigList(listOfFields, listOfFieldsForSearch, lstFacetGroupStrFields, lstFacetGroupDatesFields, listOfFacets, listOfSortFields);
                    SetDefaultValue(jobDeatil, listOfFields, listOfFieldsForSearch, listOfSortFields);
                    // var notcommonField = pageItem.ToArray().Where(w => !w.Field<bool>("isCommon"));
                    string virtualPageName = pageItem.ToArray().Length > 0 ? pageItem.ToArray()[0].Field<string>("VirtualPageName") : null;
                    foreach (var pItem in pageItem)
                        AddUpdateFieldsInTab(out indexType, out tabName, listOfFields, listOfFieldsForSearch, lstFacetGroupStrFields, lstFacetGroupDatesFields, listOfFacets, listOfSortFields, pItem);
                    var existingVirtualPage = findVirtualConfig(virtualPage, pageItem.Key.ToString());
                    existingVirtualPage = existingVirtualPage == null ? virtualPage[0] : existingVirtualPage;
                    var config = GetConfig(iUtilityFunctions, objProjectConfig, defaultClientConfig, jobDeatil,
                               listOfFields, listOfFieldsForSearch, lstFacetGroupStrFields, lstFacetGroupDatesFields, listOfFacets, listOfSortFields, existingVirtualPage.config);
                    config.indexName = iUtilityFunctions.GetIndexName(jobDeatil.IndexType, clientId, iUtilityFunctions.GetIndexPrefix(jobDeatil, currentTenant));
                    config.displayTemplateName = existingTab == null ? jobDeatil.IndexType : objtabConfig.config.displayTemplateName;
                    //defaultPageRefId = pageItem.Key.ToString();
                    existingVirtualPage.name = virtualPageName;
                    existingVirtualPage.displayName = virtualPageName;
                    existingVirtualPage.pageReferenceId = pageItem.Key.ToString();
                    existingVirtualPage.config = config;
                    listOfVirtualPage.Add(JsonConvert.DeserializeObject(JsonConvert.SerializeObject(existingVirtualPage)));
                }
                multiPage.virtualPages = JArray.Parse(JsonConvert.SerializeObject(listOfVirtualPage));
                multiPage.label = multiPage.label;
                multiPage.header = multiPage.header;
                multiPage.showHeader = true;
                multiPage.showAllSection = true;
                multiPage.defaultPageRefId = multiPage.defaultPageRefId;
                objtabConfig.multiPageSetting = multiPage;
            }
            else
            {
                objtabConfig.multiPageSetting = null;
                //objtabConfig.multiPageSetting = existingTab == null ? null : objtabConfig.multiPageSetting;
            }
            objtabConfig.multiPage = isMultiPage;
            objtabConfig.PageId = pageId;
            // objtabConfig = AddAttributesIfNotExist(objtabConfig, defaultClientConfig.projects[0].Tabs[0]);
            if (existingTab != null)
            {
                int count = 0;
                dynamic existingList = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(lstTabsConfig));
                foreach (var lstItem in existingList)
                {
                    if (lstItem.PageId == existingTab.PageId && lstItem.Tab == existingTab.Tab)
                    {
                        lstTabsConfig[count] = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(objtabConfig));
                        break;
                    }
                    count++;
                }
                existingList = null;
            }
            else
                lstTabsConfig.Add(objtabConfig);
        }
        private void ClearConfigList(List<dynamic> listOfFields, List<dynamic> listOfFieldsForSearch, List<dynamic> lstFacetGroupStrFields,
            List<dynamic> lstFacetGroupDatesFields, List<dynamic> listOfFacets, List<dynamic> listOfSortFields)
        {
            listOfFields.Clear();
            listOfFieldsForSearch.Clear();
            lstFacetGroupStrFields.Clear();
            lstFacetGroupDatesFields.Clear();
            listOfFacets.Clear();
            listOfSortFields.Clear();
        }
        private void SetDefaultValue(JobDeatil jobDeatil, List<dynamic> listOfFields, List<dynamic> listOfFieldsForSearch, List<dynamic> listOfSortFields)
        {
            listOfSortFields.Add(new { direction = "", name = "Relevance", value = "", selected = "true", sequence = 1, isActive = true });
            if (jobDeatil.IsAttachment)
            {
                listOfFieldsForSearch.Add(new { name = "Content", isActive = true, displayName = "Document", useForSearchIn = true });
                listOfFields.Add(new { name = "isPreview", isActive = true });
                listOfFields.Add(new { name = "documentPreviewPath", isActive = true });
                listOfFields.Add(new { name = "fileSize", isActive = true });
            }
            // for all 
            listOfFields.Add(new { name = "NEREntities", isActive = true });
        }

        private dynamic GetConfig(IUtilityFunctions iUtilityFunctions, dynamic objProjectConfig,
           dynamic defaultClientConfig, JobDeatil jobDeatil, List<dynamic> listOfFields, List<dynamic> listOfFieldsForSearch,
           List<dynamic> lstFacetGroupStrFields, List<dynamic> lstFacetGroupDatesFields, List<dynamic> listOfFacets,
           List<dynamic> listOfSortFields, dynamic config)
        {
            if (config != null && config.facets != null && config.facets.Count > 0)
            {
                dynamic existingFacets = JArray.Parse(JsonConvert.SerializeObject(config.facets));
                updateExistingIsSticky(existingFacets, listOfFacets);
                existingFacets = null;
            }
            config.facets = JArray.Parse(JsonConvert.SerializeObject(listOfFacets));
            if (config != null && config.resultFields != null && config.resultFields.Count > 0)
            {
                dynamic existingResultFields = JArray.Parse(JsonConvert.SerializeObject(config.resultFields));
                updateExistingResultField(existingResultFields, listOfFields);
                existingResultFields = null;
            }
            config.resultFields = JArray.Parse(JsonConvert.SerializeObject(listOfFields));
            //if (config != null && config.fieldsToPerformSearch != null && config.fieldsToPerformSearch.Count > 0)
            //[Multipage issue] : condition not working for exclude fields, for firsttime
            if (config != null && config.fieldsToPerformSearch != null)
            {
                dynamic existingFieldsForSearch = JArray.Parse(JsonConvert.SerializeObject(config.fieldsToPerformSearch));
                updateExistingFieldToPerformSearch(existingFieldsForSearch, listOfFieldsForSearch, jobDeatil.ExcludeFieldsToPerformSearch);
                existingFieldsForSearch = null;

            }
            config.fieldsToPerformSearch = JArray.Parse(JsonConvert.SerializeObject(listOfFieldsForSearch));
            //combine dates and tring columns 
            lstFacetGroupStrFields.AddRange(lstFacetGroupDatesFields.ToArray());

            if (config != null && config.facetsGroup != null && config.facetsGroup.Count > 0)
            {
                dynamic existingFacetGroups = JArray.Parse(JsonConvert.SerializeObject(config.facetsGroup));
                config.facetsGroup = updateExistingFacetGroupSequence(existingFacetGroups, lstFacetGroupStrFields);
                existingFacetGroups = null;
            }

            config.facetsGroup[0].fields = JArray.Parse(JsonConvert.SerializeObject(lstFacetGroupStrFields));

            if (config != null && config.sortFields != null && config.sortFields.Count > 0)
            {
                dynamic existingsortFields = JArray.Parse(JsonConvert.SerializeObject(config.sortFields));
                updateExistingSortField(existingsortFields, listOfSortFields);
                existingsortFields = null;
            }
            config.sortFields = JArray.Parse(JsonConvert.SerializeObject(listOfSortFields));
            config = AddAttributesIfNotExist(config, defaultClientConfig.projects[0].tabs[0].config);

            return config;

        }
        private void updateExistingIsSticky(dynamic existingFacets, dynamic currentFacets)
        {
            dynamic facets = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(currentFacets));
            foreach (var item in existingFacets)
            {
                int index = 0;
                foreach (var facet in facets)
                {
                    if (item.field == facet.field)
                    {
                        currentFacets[index] = new { field = facet.field, type = facet.type, issticky = item.issticky };
                        break;
                    }
                    index++;
                }
            }
            facets = null;

        }
        private void updateExistingFieldToPerformSearch(dynamic existingFields, dynamic currentFields, string excludeFieldsToPerformSearch)
        {
            dynamic fields = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(currentFields));
            int index = 0;
            var excludefields = excludeFieldsToPerformSearch?.Split(",");
            foreach (var item in fields)
            {
                if (excludefields != null)
                {
                    string field = Convert.ToString(item.name);
                    if (excludefields.Contains(field))
                    {
                        currentFields.RemoveAt(index);
                        continue;
                    }
                }

                foreach (var eField in existingFields)
                {
                    if (Convert.ToString(eField.Type) == "String")
                        continue;
                    if (item.name == eField.name)
                    {
                        currentFields[index] = new
                        {
                            name = eField.name,
                            isActive = eField.isActive,
                            displayName = eField.displayName == null ? item.displayName : eField.displayName,
                            sequenceNo = eField.sequenceNo == null ? item.sequenceNo : eField.sequenceNo,
                            useForSearchIn = eField.useForSearchIn == null ? item.useForSearchIn : eField.useForSearchIn,
                        };
                        break;
                    }
                }
                index++;
            }
            fields = null;
        }
        private void updateExistingResultField(dynamic existingFields, dynamic currentFields)
        {
            dynamic fields = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(currentFields));
            int index = 0;
            foreach (var item in fields)
            {
                foreach (var eField in existingFields)
                {
                    if (Convert.ToString(eField.Type) == "String")
                        continue;
                    if (item.name == eField.name)
                    {
                        currentFields[index] = new { name = eField.name, isActive = eField.isActive };
                        break;
                    }
                }
                index++;
            }
            fields = null;
        }

        private void updateExistingSortField(dynamic existingSorts, dynamic currentSorts)
        {
            dynamic sorts = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(currentSorts));
            int index = 0;
            foreach (var item in sorts)
            {
                foreach (var eField in existingSorts)
                {
                    if (item.value == eField.value)
                    {
                        if (item.type == "date")
                            currentSorts[index] = new
                            {
                                direction = item.direction,
                                name = (eField.name == null || eField.name == "") ? item.name : eField.name,
                                value = item.value,
                                sequence = eField.sequence == null ? 1 : eField.sequence,
                                isActive = eField.isActive == null ? true : eField.isActive,
                                type = item.type,
                                selected = item.selected == null ? "false" : item.selected
                            };
                        else
                            currentSorts[index] = new
                            {
                                direction = item.direction,
                                name = (eField.name == null || eField.name == "") ? item.name : eField.name,
                                value = item.value,
                                sequence = eField.sequence == null ? 1 : eField.sequence,
                                isActive = eField.isActive == null ? true : eField.isActive,
                                selected = item.selected == null ? "false" : item.selected
                            };
                        break;
                    }

                }
                index++;

            }
            sorts = null;
        }

        private dynamic updateExistingFacetGroupSequence(dynamic existingFacetGroups, dynamic currentFacetGroups)
        {
            dynamic facetGroups = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(currentFacetGroups));
            dynamic existingFacetGroupsCopy = JArray.Parse(JsonConvert.SerializeObject(existingFacetGroups));

            bool matchedField = false;
            foreach (var item in facetGroups)
            {
                matchedField = false;
                int groupCount = 0;
                foreach (var fields in existingFacetGroups)
                {
                    dynamic facetFields = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(existingFacetGroupsCopy[groupCount].fields));

                    if (updateFacetFields(item, fields, facetFields))
                    {
                        matchedField = true;
                        existingFacetGroupsCopy[groupCount].fields = JArray.Parse(JsonConvert.SerializeObject(facetFields));
                    }
                    facetFields = null;
                    groupCount++;
                }
                if (!matchedField)
                {
                    dynamic facetFields = JsonConvert.DeserializeObject<List<dynamic>>(JsonConvert.SerializeObject(existingFacetGroupsCopy[0].fields));
                    facetFields.Add(getFacetField(item, item));
                    existingFacetGroupsCopy[0].fields = JArray.Parse(JsonConvert.SerializeObject(facetFields));
                    facetFields = null;
                }
            }
            facetGroups = null;
            return existingFacetGroupsCopy;

        }
        private bool updateFacetFields(dynamic newfields, dynamic fields, dynamic facetFields)
        {
            int fieldCount = 0;
            bool fieldMatched = false;
            foreach (var eField in fields.fields)
            {
                if (newfields.name == eField.name)
                {
                    if (newfields.enableCalendar != null)
                        facetFields[fieldCount] = getFacetField(newfields, eField);
                    else
                        facetFields[fieldCount] = getFacetField(newfields, eField);
                    fieldMatched = true;
                    break;
                }
                fieldCount++;
            }
            return fieldMatched;

        }
        private dynamic getFacetField(dynamic newfields, dynamic eField)
        {
            if (newfields.enableCalendar != null)
                return new
                {
                    label = (eField.label == null || eField.label == "") ? newfields.label : eField.label,
                    name = newfields.name,
                    type = newfields.type,
                    sequence = eField.sequence == null ? 1 : eField.sequence,
                    isActive = eField.isActive == null ? true : eField.isActive,
                    enableCalendar = eField.enableCalendar == null ? newfields.enableCalendar : eField.enableCalendar
                };
            else
                return new
                {
                    label = (eField.label == null || eField.label == "") ? newfields.label : eField.label,
                    name = newfields.name,
                    type = newfields.type,
                    sequence = eField.sequence == null ? 1 : eField.sequence,
                    isActive = eField.isActive == null ? true : eField.isActive,
                };
        }

        /// <summary>
        /// updates refiners,sort fields, refiner groups, results fields and fieldstoperformsearch
        /// </summary>
        /// <param name="indexType"></param>
        /// <param name="tabName"></param>
        /// <param name="listOfFields"></param>
        /// <param name="listOfFieldsForSearch"></param>
        /// <param name="lstFacetGroupStrFields"></param>
        /// <param name="lstFacetGroupDatesFields"></param>
        /// <param name="listOfFacets"></param>
        /// <param name="listOfSortFields"></param>
        /// <param name="tabItem"></param>
        private void AddUpdateFieldsInTab(out string indexType, out string tabName, List<dynamic> listOfFields, List<dynamic> listOfFieldsForSearch, List<dynamic> lstFacetGroupStrFields,
            List<dynamic> lstFacetGroupDatesFields, List<dynamic> listOfFacets, List<dynamic> listOfSortFields, DataRow tabItem)
        {
            string fieldName = tabItem.Field<string>("FieldName");
            string fieldDisplayName = tabItem.Field<string>("DisplayName");
            indexType = tabItem.Field<string>("PageName").ToLower();
            //tabName = tabItem.Field<string>("PageNameToShow");
            tabName = tabItem.Field<string>("VirtualTabName");
            if (tabItem.Field<string>("FieldType").ToLower() == "string")
            {
                if (!IfExistInList(listOfFieldsForSearch, fieldName))
                    listOfFieldsForSearch.Add(new { name = fieldName, isActive = true, displayName = fieldDisplayName, sequenceNo = 1, useForSearchIn = true });
                if (tabItem.Field<bool>("isDynamicField"))
                {
                    if (!IfExistInList(listOfFacets, fieldName))
                        listOfFacets.Add(new { field = fieldName, type = "value", issticky = true });
                    if (!IfExistInList(lstFacetGroupStrFields, fieldName))
                        lstFacetGroupStrFields.Add(new { label = fieldDisplayName, name = fieldName, type = "multi", sequence = 1, isActive = true });
                    if (tabItem.Field<bool>("isSort"))
                    {
                        if (!IfExistInList(listOfSortFields, fieldDisplayName))
                            listOfSortFields.Add(new { direction = "asc", name = fieldDisplayName, value = fieldName, sequence = listOfSortFields.Count + 1, isActive = true });
                    }
                }
                else
                {
                    if (tabItem.Field<bool>("isSort"))
                    {
                        if (!IfExistInList(listOfSortFields, fieldDisplayName))
                            listOfSortFields.Add(new { direction = "asc", name = fieldDisplayName, value = fieldName, sequence = listOfSortFields.Count + 1, isActive = true });
                    }
                }
            }
            else if (tabItem.Field<string>("FieldType").ToLower() == "date")
            {
                if (tabItem.Field<bool>("isSort"))
                {
                    if (!IfExistInList(listOfSortFields, fieldDisplayName))
                        listOfSortFields.Add(new { direction = "desc", name = fieldDisplayName, value = fieldName, type = "date", sequence = listOfSortFields.Count + 1, isActive = true });
                }

                //if(tabItem.Field<bool>("isDynamicField"))
                {
                    if (!IfExistInList(listOfFacets, fieldName))
                        listOfFacets.Add(new { field = fieldName, type = "date", issticky = false });
                    if (!IfExistInList(lstFacetGroupDatesFields, fieldName))
                        lstFacetGroupDatesFields.Add(new { label = fieldDisplayName, name = fieldName, type = "single", enableCalendar = true, sequence = 2, isActive = true });
                }
            }
            if (!IfExistInList(listOfFields, fieldName))
                listOfFields.Add(new { name = fieldName, isActive = true });
        }

        private bool IfExistInList(List<dynamic> lst, string value)
        {
            bool flag = false;
            Func<dynamic, string, bool> IsPropertyExist = (setting, name) => { return setting.GetType().GetProperty(name) != null; };
            foreach (var item in lst)
            {
                flag = IsPropertyExist(item, "name") ? item.name == value :
                    IsPropertyExist(item, "field") ? item.field == value : false;
                if (flag)
                    break;
            }
            return flag;
        }


        private dynamic GetConfigForClient()
        {
            dynamic templateItem = null;
            var templateConfigJSON = System.IO.File.ReadAllText(@"Config\SLBDefaultConfig.json");
            if (templateConfigJSON != null)
            {
                templateItem = JValue.Parse(templateConfigJSON);
            }
            return templateItem;
        }
        private string ToJson(BsonDocument bson)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BsonIO.BsonBinaryWriter(stream))
                {
                    BsonSerializer.Serialize(writer, typeof(BsonDocument), bson);
                }
                stream.Seek(0, SeekOrigin.Begin);
                using (var reader = new Newtonsoft.Json.Bson.BsonDataReader(stream))
                {
                    var sb = new StringBuilder();
                    var sw = new StringWriter(sb);
                    using (var jWriter = new JsonTextWriter(sw))
                    {
                        jWriter.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
                        jWriter.WriteToken(reader);
                    }
                    return sb.ToString();
                }
            }
        }


    }
}