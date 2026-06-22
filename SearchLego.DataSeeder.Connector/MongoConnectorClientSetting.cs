using Microsoft.Extensions.Logging;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;


namespace SearchLego.DataSeeder.Connector
{
    public class MongoConnectorClientSetting : MongoConfigDataRepository
    {
        public override IMongoConfigFactory GetDBObject(ILogger<dynamic> logger, ApplicationConfig configDetail, string collectionName)
        {
            string DBName = GetDatabaseName(configDetail.TenantDBName);
            IMongoDatabaseFactory mongoDatabaseFactory = new MongoDatabaseFactory(logger, configDetail);
            return new MongoConfigUpdate(mongoDatabaseFactory.CreateDatabase(configDetail, null, DBName, true), collectionName);
        }
        public static string GetDatabaseName(string DatabaseName)
        {
            return DatabaseName;
        }



    }
}
