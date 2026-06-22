using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Entities.Cognitive;
using SearchLego.DataSeeder.Host;
using System.Collections.Generic;

namespace SearchLego.DataSeeder.Elastic
{
    public interface ICognitiveDataElasticService
    {
        bool GenerateDataSummary(JobDeatil jobDetail, JobProcessInfo jobProcessInfo, ApplicationConfig applicationConfig, IUtilityFunctions utilityFunctions, JobTrackingUpdateInfo jobTrackingUpdateInfo ,JobProcessType jobProcessType, JobTracking client, dynamic dbJobTracking, IMongoConfigFactory jobHistoryFactory, IMongoConfigFactory JobTrackingFactory);
        bool FetchAndUpdateNEREntities(JobDeatil jobDetail, JobProcessInfo jobProcessInfo, ApplicationConfig applicationConfig, IUtilityFunctions utilityFunctions, JobTrackingUpdateInfo jobTrackingUpdateInfo, IMongoConfigFactory nerEntityRulerData,JobProcessType jobProcessType, JobTracking client_jt, dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory);
        bool FetchAndUpdateAutoSuggestions(JobDeatil jobDetail, JobProcessInfo jobProcessInfo, ApplicationConfig applicationConfig, IUtilityFunctions utilityFunctions, JobTrackingUpdateInfo jobTrackingUpdateInfo, JobProcessType jobProcessType, JobTracking client_jt, dynamic dbJobTracking, IMongoConfigFactory JobTrackingFactory, IMongoConfigFactory jobHistoryFactory);
        DataDictionaryResponse DataDictionaryIngestClientWise(JobProcessInfo jobProcessInfo, ApplicationConfig applicationConfig,string baseIndexName, IUtilityFunctions utilityFunctions, JobTrackingUpdateInfo jobTrackingUpdateInfo, JobTracking client, dynamic dbJobTracking, JobProcessType processType, IMongoConfigFactory jobHistoryFactory, IMongoConfigFactory JobTrackingFactory);
        bool DeleteOldDataDictionaryIndexes(string pattern, List<string> activeIndexes);
    }
}
