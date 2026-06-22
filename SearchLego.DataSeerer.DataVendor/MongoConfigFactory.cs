using Microsoft.Extensions.Logging;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;

namespace SearchLego.DataSeerer.Integration
{
    public class MongoConfigFactory : MongoConfigDataRepository
    {
        public override IMongoConfigFactory GetDBObject(ILogger<dynamic> logger, ApplicationConfig configDetail, string collectionName)
        {
            IMongoDatabaseFactory mongoDatabaseFactory = new MongoDatabaseFactory(logger, configDetail);
            return new MongoConfigUpdate(mongoDatabaseFactory.CreateDatabase(configDetail,null), collectionName);
        }
    }
}
