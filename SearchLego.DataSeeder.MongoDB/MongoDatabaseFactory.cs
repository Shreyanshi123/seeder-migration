using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using SearchLego.DataSeeder.Azure;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using System;
using System.Linq;

namespace SearchLego.DataSeeder.MongoDB
{
    public sealed class MongoDatabaseFactory : IMongoDatabaseFactory, IDisposable
    {
        public static string MongoConnectionString { get; set; }
        public string DefaultDatabase { get; }
        private static MongoClient _client;
        private static readonly object padlock = new object();
        private IAzureVaultRepository azureVaultRepository;

        public MongoDatabaseFactory()
        {

        }

        public MongoDatabaseFactory(ILogger<dynamic> logger, ApplicationConfig configDetail)
        {
            // ToDo: add mongo authentication
            if (string.IsNullOrEmpty(MongoConnectionString))
            {
                if (configDetail.IsAzureVaultEnabled)
                {
                    azureVaultRepository = new AzureVaultRepository(logger);
                    MongoConnectionString = azureVaultRepository.GetSecretValue(configDetail.AzureVaultKeys?.MongoConnectionSecretName);
                }
                else
                {
                    MongoConnectionString = configDetail.SLBConfigConnectionString;
                }
            }

            DefaultDatabase = configDetail.SLBConfigDataBase;
            lock (padlock)
            {
                if (_client == null)
                {
                    _client = new MongoClient(MongoConnectionString);
                }
            }
        }

        #region Implementation of IMongoDatabaseFactory

        public IMongoDatabase CreateDatabase(ApplicationConfig applicationConfig, string tenantId, string name = null, bool isSharedConnection = false)
        {
            if (name == null)
            {
                name = DefaultDatabase;
            }
            if (applicationConfig.TenantType.Equals(TenantConnectionType.Multi.ToString()) && !isSharedConnection && tenantId != null)
            {
                string collectionDBName = ApplicationConfig.TenantCollections?.Where(t => t.TenantId == tenantId).
                                                           Select(n => n.DBCollectionName)?.FirstOrDefault();

                return _client.GetDatabase(string.IsNullOrEmpty(collectionDBName) ? name : collectionDBName);
            }
            else
                return _client.GetDatabase(name);
        }

        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
        }

        #endregion
    }

    public interface IMongoDatabaseFactory
    {
        /// <summary>
        /// Obtain access to the <see cref="IMongoDatabase"/> object by the logical database name.
        /// </summary>
        /// <param name="name">Logical database name. Null for default database.</param>
        /// <returns></returns>
        IMongoDatabase CreateDatabase(ApplicationConfig applicationConfig, string clientId, string name = null, bool isSharedConnection = false);
    }
}
