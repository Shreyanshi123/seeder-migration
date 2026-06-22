using Microsoft.Extensions.Logging;
using SearchLego.DataSeeder.Azure;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;
using SearchLego.DataSeeder.PostgressSQL;
using SearchLego.DataSeeder.SQL;

namespace SearchLego.DataSeerer.Integration
{
    public class DataSourceFactory : IDataObjectFactory
    {
        IDataSourceFactory _iDataFactory = null;
        private IAzureVaultRepository azureVaultRepository;
        public static string connectionString { get; set; }

        public DataSourceFactory(ILogger<dynamic> _logger)
        {
            azureVaultRepository = new AzureVaultRepository(_logger);
        }

        public override IDataSourceFactory GetDBObject(ApplicationConfig configDetail)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = configDetail.IsAzureVaultEnabled ? azureVaultRepository.GetSecretValue(configDetail.AzureVaultKeys?.SqlDataConnectionSecretName) :
                    configDetail.DataConnectionString;
            }

            switch (configDetail.ServerType)
            {
                case ServerType.SQLServer:
                    _iDataFactory = new SQLDataRepository(connectionString);
                    break;
                case ServerType.PostgressSQL:
                    _iDataFactory = new PostgressDataRepository(connectionString);
                    break;
                case ServerType.Mongo:
                    _iDataFactory = new MongoDataRepository(connectionString);
                    break;

            };
            return _iDataFactory;
        }
    }
}
