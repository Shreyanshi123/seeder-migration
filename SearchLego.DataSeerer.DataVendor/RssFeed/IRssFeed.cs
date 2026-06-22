using SearchLego.DataSeeder.Entities;

namespace SearchLego.DataSeerer.Integration.RssFeed
{
    public interface IRssFeed
    {
        JobTrackingUpdateInfo GenerateRssFeed(string jobId, JobProcessInfo jobProcessInfo);
    }
}
