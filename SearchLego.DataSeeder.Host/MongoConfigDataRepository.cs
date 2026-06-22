using SearchLego.DataSeeder.Common;
using Microsoft.Extensions.Logging;

namespace SearchLego.DataSeeder.Host
{
    public abstract class MongoConfigDataRepository
    {
        public abstract IMongoConfigFactory GetDBObject(ILogger<dynamic> logger, ApplicationConfig configDetail, string collectionName);

    }
}
