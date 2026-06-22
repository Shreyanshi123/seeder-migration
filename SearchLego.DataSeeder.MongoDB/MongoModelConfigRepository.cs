using MongoDB.Bson;
using MongoDB.Driver;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.MongoDB.Host;

namespace SearchLego.DataSeeder.MongoDB
{
    public class MongoModelConfigRepository : IMongoModelConfigRepository
    {
        protected IMongoCollection<BsonDocument> Collection;
        public MongoModelConfigRepository(IMongoDatabase database, string collectionName)
        {
            Collection = database.GetCollection<BsonDocument>(collectionName);
        }

        public BsonDocument GetModelConfig(int clientId, int projectId, string jobName,bool isLatest)
        {
            var query = Builders<BsonDocument>.Filter.Eq(Constants.CLIENT_ID, clientId);
            query &= Builders<BsonDocument>.Filter.Eq(Constants.PROJECT_ID, projectId);
            query &= Builders<BsonDocument>.Filter.Eq(Constants.ML_MODEL_CONFIG_JOBNAME, jobName);
            query &= Builders<BsonDocument>.Filter.Eq(Constants.ML_MODEL_CONFIG_ISLATEST, isLatest);

            return Collection.Find(query).FirstOrDefault();
        }
    }
}
