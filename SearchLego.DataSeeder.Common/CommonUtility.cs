using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Nest;
using Newtonsoft.Json;
using SearchLego.DataSeeder.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Job = SearchLego.DataSeeder.Entities.Job;

namespace SearchLego.DataSeeder.Common
{
    public class CommonUtility
    {
        private readonly static Object lockJobTracking = new Object();
        public static DataTable ConvertDataTableFieldType(DataTable dt, string DefaultValue)
        {
            DataTable dtCloned = dt.Clone();
            for (int i = 0; i < dtCloned.Columns.Count; i++)
            {
                if (dtCloned.Columns[i].DataType != typeof(DateTime) &&
                    dtCloned.Columns[i].DataType != typeof(Int32) &&
                    dtCloned.Columns[i].DataType != typeof(Int64) &&
                    dtCloned.Columns[i].DataType != typeof(float) &&
                    dtCloned.Columns[i].DataType != typeof(double) &&
                    dtCloned.Columns[i].DataType != typeof(decimal) &&
                    dtCloned.Columns[i].DataType != typeof(long)
                    )
                    dtCloned.Columns[i].DataType = typeof(string);
            }
            foreach (DataRow row in dt.Rows)
            {
                for (int i = 0; i < dtCloned.Columns.Count; i++)
                {
                    if (dt.Columns[i].DataType == typeof(string))
                    {
                        if (row[dtCloned.Columns[i].ColumnName] == DBNull.Value || row[dtCloned.Columns[i].ColumnName].ToString() == "" || row[dtCloned.Columns[i].ColumnName].ToString().Trim().ToUpper() == "N/A")
                            row[dtCloned.Columns[i].ColumnName] = DefaultValue;
                    }
                    if (dt.Columns[i].DataType == typeof(Int32))
                    {
                        if (row[dtCloned.Columns[i].ColumnName] == DBNull.Value)
                            row[dtCloned.Columns[i].ColumnName] = 0;
                    }
                }
                dtCloned.ImportRow(row);
            }
            dtCloned.AcceptChanges();
            return dtCloned;
        }
        public static void CopyStream(Stream stream, string destPath)
        {
            using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fileStream);
            }
        }

        public static byte[] ConvertStreamToByte(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            if (input == null)
                return buffer;
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public static string ConcatenateValue(string seperator, string excludeField, Dictionary<string, object> items)
        {
            string[] fields = excludeField?.Split(",");
            StringBuilder result = new StringBuilder();
            foreach (var item in items)
            {
                //item.Value
                if (!fields.Contains(item.Key))
                {
                    object type;
                    items.TryGetValue(item.Key, out type);
                    string dataType = (type != null) ? type.GetType().Name : "string";
                    if (dataType.ToLower() == "string")
                    {
                        result.Append(item.Value);
                        result.Append(seperator);
                    }
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// This method is used to Clean/Preprocessed the data before sending data to api
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string CleanData(string data)
        {
            string bulletCharacters = "167;2022;8213;8226;8227;8259;8268;8269;8729;9675;9688;9689;9702;9753;10085;10087;10686;10687"; //— (8212)
            var bulletList = bulletCharacters.Split(";").Where(x => x.Length > 0).Select(x => ((char)Convert.ToInt32(x)).ToString()).ToList();
            var _rep = Regex.Replace(data, @"\r\n\s*\n+", @"@@REP@@");
            _rep = _rep.Replace("\n", "@@REPCHAR@@");
            _rep = _rep.Replace("\n", " ");
            _rep = _rep.Replace("@@REPCHAR@@", "\n");
            _rep = _rep.Replace("@@REP@@", "\n");
            _rep = _rep.Replace("— ", "\n— ");
            foreach (string bullet in bulletList)
                _rep = _rep.Replace(bullet, "\n" + bullet + " ");
            _rep = Regex.Replace(_rep, @"(\d+\.\s+\w+)", "\n $1"); //regex for ordered list...
            _rep = Regex.Replace(_rep, @"\s", " ");
            _rep = _rep.Replace("<", "&lt;");
            _rep = _rep.Replace(">", "&gt;");
            _rep = Regex.Replace(_rep, @"\●", "");
            _rep = Regex.Replace(_rep, @"\s+", " ");
            return _rep;
        }

        public static bool IsExecutionRequiredForClient(JobProcessInfo jobProcessInfo, string JobType)
        {
            bool isRequired = false;

            if (jobProcessInfo.isForcedExecution)
            {
                return true;
            }
            if (JobType == Constants.MODEL_INGEST && string.IsNullOrEmpty(jobProcessInfo.LastExecutedTime))
            {
                isRequired = true;

            }
            else if (JobType == Constants.DICTIONARY && string.IsNullOrEmpty(jobProcessInfo.ClientAdditionalSetting?.currentIndex))
            {
                isRequired = true;
            }
            else
            {
                DateTime lastUpdated;
                var isValidDateTime = DateTime.TryParse(jobProcessInfo.LastExecutedTime, out lastUpdated);
                if (isValidDateTime)
                {
                    var totalDays = (DateTime.Now - lastUpdated).TotalDays;
                    if (totalDays > 1.0)
                    {
                        isRequired = true;
                    }
                }
            }

            return isRequired;
        }

        public static bool UpdateStatusAsInitial(string FieldName, ElasticClient elasticClient)
        {
            var query = string.Empty;
            query = $"ctx._source." + FieldName + Constants.EQUAL_TO + "'" + Constants.INITIAL_STATE + "';";
            var resp = elasticClient.UpdateByQuery<dynamic>(s => s
                               .Script(script =>
                                   script.Source(query)));
            return resp.IsValid;
        }

        public static JobProcessInfo GetFeatureConfiguration(JobProcessType processType, dynamic jobTracking, dynamic featureConfiguration)
        {
            var featureConfig = BsonSerializer.Deserialize<FeatureConfiguration>(featureConfiguration);
            JobProcessInfo jobProcessInfo = new JobProcessInfo();
            var job = (BsonDocument)jobTracking;
            var client = BsonSerializer.Deserialize<JobTracking>(job);
            if (client.enabled)
            {
                var clientJob = client.jobs.FirstOrDefault(x => x.name == processType.ToString());
                if (clientJob != null && clientJob.enabled)
                {
                    jobProcessInfo = new JobProcessInfo()
                    {
                        FeatureConfigurationSetting = featureConfig ?? new FeatureConfiguration(),
                        ClientAdditionalSetting = clientJob.additionalSetting ?? new AdditionalSetting(),
                        ClientId = client.clientId,
                        IndexTypes = client.indexTypes,
                        LastExecutedTime = clientJob.lastExecutedTime,
                        isForcedExecution = clientJob.isForcedExecution
                    };
                }

            }
            return jobProcessInfo;
        }

        public static void UpdateJobByProcessType(JobTracking p_JobTracking, dynamic dbJobTracking, JobProcessType processType,
            JobTrackingUpdateInfo jobTrackingUpdateInfo, ApplicationConfig appConfig, dynamic jobHistoryFactory, dynamic jobTrackingfactory,string jobResponse)
        {
            if (!jobTrackingUpdateInfo.SkipUpdate)
            {
                var dbJobTrack = (BsonDocument)dbJobTracking;
                var jobTracking = BsonSerializer.Deserialize<JobTracking>(dbJobTrack);
                var clientJob = jobTracking.jobs.Find(x => x.name == processType.ToString());
                if (clientJob != null)
                {

                    // clientJob is set as reference in calling method.Its reference is present in the jobTracking. 
                    if (jobTrackingUpdateInfo.Status)
                    {
                        clientJob.lastExecutedTime = jobTrackingUpdateInfo.LastExecutedTime > DateTime.MinValue ? jobTrackingUpdateInfo.LastExecutedTime.ToString() :  DateTime.Now.ToString();
                        clientJob.status = Constants.SUCCESS;
                        clientJob.isForcedExecution = false;
                    }
                    else
                    {
                        clientJob.status = Constants.FAILED;
                        clientJob.lastExecutedTime = DateTime.Now.ToString();
                    }

                    if (processType == JobProcessType.DataDictionary && jobTrackingUpdateInfo.Status)
                    {
                        clientJob.additionalSetting.currentIndex = jobTrackingUpdateInfo.CurrentIndex;
                        clientJob.additionalSetting.previousIndex = jobTrackingUpdateInfo.PreviousIndex;
                    }

                    if(processType == JobProcessType.FileIngestion && jobTrackingUpdateInfo.Status)
                    {
                        if(clientJob.additionalSetting == null)
                        {
                            clientJob.additionalSetting = new AdditionalSetting();
                        }
                        clientJob.additionalSetting.sourceExecutionInfos = jobTrackingUpdateInfo.sourceExecutionInfos;
                    }

                    var _jobTracking = BsonSerializer.Deserialize<BsonDocument>(JsonConvert.SerializeObject(jobTracking));
                    if (jobTracking != null)
                        jobTrackingfactory.Update(_jobTracking);

                    if (processType != JobProcessType.DataDictionary && processType != JobProcessType.NEREntities)
                    {
                        InsertJobHistory(clientJob, jobTrackingUpdateInfo, p_JobTracking, jobHistoryFactory, appConfig, jobResponse);
                    }
                }

            }

        }

        public static void InsertJobHistory(Job clientJob, JobTrackingUpdateInfo jobTrackingUpdateInfo, JobTracking p_JobTracking, dynamic jobHistoryFactory, ApplicationConfig appConfig,string jobResponse)
        {

            if (appConfig.EnableJobHistory)
            {
                var jobHistory = new JobHistory
                {
                    clientId = p_JobTracking.clientId,
                    jobId = p_JobTracking._id,
                    featureType = clientJob.name,
                    lastExecutedTime = clientJob.lastExecutedTime,
                    status = clientJob.status,
                    noOfRecordsUpdated = jobTrackingUpdateInfo.NoOfUpdatedRecords.ToString(),
                    jobHistoryMessage = jobResponse
                };

                var _jobHistory = BsonSerializer.Deserialize<BsonDocument>(JsonConvert.SerializeObject(jobHistory));
                if (_jobHistory != null)
                    jobHistoryFactory.Add(_jobHistory);
            }
        }
    }
    public static class Converter
    {
        public static T ConvertBsonToObject<T>(this BsonDocument bson)
        {
            return BsonSerializer.Deserialize<T>(bson);
        }
    }
}
