using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.NER;
using System;

namespace SearchLego.DataSeeder.Elastic
{
    public class ModelIngestService : IModelIngestService
    {
        private readonly ILogger<dynamic> _logger;

        public ModelIngestService(ILogger<dynamic> logger)
        {
            _logger = logger;
        }

        public ModelConfig GenerateModelFile(object data, string baseApi, string url)
        {
            ModelConfig resultItem = null;

            _logger.LogInformation($"Generate Model File called with data : {data} and baseApi : {baseApi} and url : {url}");
            try
            {
                using (var iNERProcess = new NERProcess())
                {
                    string result = iNERProcess.NERDataProcess(data, baseApi, url);
                    resultItem = JsonConvert.DeserializeObject<ModelConfig>(result);
                }

                _logger.LogInformation($"Generate Model File call completed for data : {data} and baseApi : {baseApi} and url : {url}");

            }
            catch (Exception ex)
            {
                _logger.LogError($"[model] Initial_Validation - Error occured getting/Deserializing model file " + "Error Message :" + ex.Message);
            }

            return resultItem;
        }
    }
}
