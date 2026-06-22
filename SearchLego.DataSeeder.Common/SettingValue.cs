using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace SearchLego.DataSeeder.Common
{
    public class SettingValue : ISettingValue
    {
        /* IMongoConfigFactory iMongoClientSetting;
         SettingValue()
         {
              = jobsDetail.EnableUISetting ? new MongoClientSetting().GetDBObject(jobsDetail) : null;


         }*/
        //private static SingletonReadWriteParamValue _intance;
        //private static readonly object _lock = new object();
        //private SingletonReadWriteParamValue() { }
        //public static SingletonReadWriteParamValue Instance
        //{
        //    get
        //    {
        //        lock (_lock)
        //        {
        //            if (_intance == null)
        //            {
        //                _intance = new SingletonReadWriteParamValue();
        //            }
        //            return _intance;
        //        }
        //    }
        //}

        /// <summary>
        /// build the config for all client/indexes
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="FileName"></param>
        /// <returns></returns>

        /*public bool BuildSettingByClientWise(List<string> lstClient, string FileName)
        {
           
            lock (this)
            {
                bool isExecutedSuccessfully = false;
                string FilePath = Path.Combine(Directory.GetCurrentDirectory(), @"Config\" + FileName);
                if (File.Exists(FilePath))
                {
                    string jsonData = File.ReadAllText(FilePath);
                    var jsonObject = JsonConvert.DeserializeObject<ParametersValue>(jsonData);
                    var result = jsonObject.jobs.Where(w => w.Id == JobId); //.Select(func);
                    return result.FirstOrDefault();
                }
                return isExecutedSuccessfully; //new Jobs();
            }
        }*/
        public Jobs ReadParameterValue(string JobId, string FileName)
        {
            lock (this)
            {

                string FilePath = Path.Combine(Directory.GetCurrentDirectory(), @"Config\" + FileName);
                if (File.Exists(FilePath))
                {
                    string jsonData = File.ReadAllText(FilePath);
                    var jsonObject = JsonConvert.DeserializeObject<ParametersValue>(jsonData);
                    var result = jsonObject.jobs.Where(w => w.Id == JobId); //.Select(func);
                    return result.FirstOrDefault();
                }
                return new Jobs();
            }
        }
        public bool WriteParameterValue(string JobId, string FileName, Action<Jobs> action)
        {
            lock (this)
            {
                bool _flag = false;
                string FilePath = Path.Combine(Directory.GetCurrentDirectory(), @"Config\" + FileName);
                if (File.Exists(FilePath))
                {
                    string jsonData = File.ReadAllText(FilePath);
                    var jsonObject = JsonConvert.DeserializeObject<ParametersValue>(jsonData);
                    var job = jsonObject.jobs.Where(w => w.Id == JobId);
                    job.ToList().ForEach(action);
                    var result = JsonConvert.SerializeObject(jsonObject);
                    File.WriteAllText(FilePath, result, System.Text.Encoding.UTF8);
                    _flag = true;
                }
                return _flag;
            }

        }

    }


}
