using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.MongoDB;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.Connector
{
    public class TenantService : ITenantService
    {

        private readonly ILogger<dynamic> _logger;

        public TenantService(ILogger<dynamic> logger)
        {
            _logger = logger;
        }
        public async Task<IList<Tenant>> GetAllTenant(TenantConnectionType tenantType, ApplicationConfig applicationConfig)
        {
            _logger.LogInformation($"GetAllTenant method called.");

            IList<Tenant> tenantList = new List<Tenant>();
            IMongoConfigFactory iMongoConfigFactory = new MongoConnectorClientSetting().GetDBObject(_logger, applicationConfig, MongoStaticName.Tenants);

            var getAllTenants = await iMongoConfigFactory.GetAllAsyn();
            var tenantsList = from tenants in getAllTenants
                              where tenants["IsActive"] == true
                              select tenants;
            foreach (var tenant in tenantsList)
            {
                var deserializeObject = BsonSerializer.Deserialize<Tenant>(tenant);
                deserializeObject.TenantType = tenantType;
                tenantList.Add(deserializeObject);
            }

            return tenantList;
        }
        public Tenant GetTenantById(string id, TenantConnectionType tenantType, ApplicationConfig applicationConfig)
        {
            _logger.LogInformation($"GetTenantById method called.");
            IMongoConfigFactory iMongoConfigFactory = new MongoConnectorClientSetting().GetDBObject(_logger,applicationConfig, MongoStaticName.Tenants);

            var tenantDetails = BsonSerializer.Deserialize<Tenant>(iMongoConfigFactory.GetById(id));
            tenantDetails.TenantType = tenantType;
            return tenantDetails;
        }


    }
}
