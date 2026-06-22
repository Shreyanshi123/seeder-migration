namespace SearchLego.DataSeeder.Common
{
    public interface IAggregate : IEntity, IVersioned
    {
    }
    public interface IEntity
    {
        string Id { get; set; }
    }
    public interface IVersioned
    {
        int Version { get; set; }
    }
}
