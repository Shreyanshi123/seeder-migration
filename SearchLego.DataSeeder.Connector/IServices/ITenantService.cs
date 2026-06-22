using MongoDB.Bson;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.Connector
{
    public interface ITenantService
    {
        Task<IList<Tenant>> GetAllTenant(TenantConnectionType tenantType, ApplicationConfig applicationConfig);
        Tenant GetTenantById(string id, TenantConnectionType tenantType, ApplicationConfig applicationConfig);
    }
}
