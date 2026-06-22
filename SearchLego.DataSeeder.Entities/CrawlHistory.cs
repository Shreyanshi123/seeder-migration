namespace SearchLego.DataSeeder.Entities
{
    public class CrawlHistory
    {
        public string ExecutionTime { get; set; }
        public int RecordsUpdated { get; set; }
        public CrawlType CrawlType { get; set; }
    }
    public enum CrawlType
    {
        Full,
        Incremental
    }
}
