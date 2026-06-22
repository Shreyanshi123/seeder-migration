using MongoDB.Bson;

namespace SearchLego.DataSeeder.MongoDB.Host
{
    public interface IMongoModelConfigRepository 
    {
        BsonDocument GetModelConfig(int clientId, int projectId, string jobName, bool isLatest);
    }
}

