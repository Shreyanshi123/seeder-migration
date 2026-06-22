using Microsoft.Extensions.Logging;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;
using SearchLego.DataSeeder.MongoDB.Host;

namespace SearchLego.DataSeerer.Integration
{
    public class MongoClientSetting : MongoConfigDataRepository
    {
        public override IMongoConfigFactory GetDBObject(ILogger<dynamic> logger, ApplicationConfig configDetail, string collectionName)
        {
            IMongoDatabaseFactory mongoDatabaseFactory = new MongoDatabaseFactory(logger, configDetail);
            return new MongoConfigUpdate(mongoDatabaseFactory.CreateDatabase(configDetail, null), collectionName);
        }

        public IMongoModelConfigRepository GetMongoConfigDBObject(ILogger<dynamic> logger, ApplicationConfig configDetail, string collectionName)
        {
            IMongoDatabaseFactory mongoDatabaseFactory = new MongoDatabaseFactory(logger, configDetail);
            return new MongoModelConfigRepository(mongoDatabaseFactory.CreateDatabase(configDetail, null), collectionName);
        }
    }

}
