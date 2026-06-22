using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;


namespace SearchLego.DataSeerer.Integration.Documents
{
    public interface IFileIngestion
    {
        JobTrackingUpdateInfo IngestFiles(string jobId, JobProcessInfo jobProcessInfo, JobDeatil jobDetail);

    }
}

