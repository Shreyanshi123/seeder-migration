using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.Entities
{
    public class Tenant
    {
        public string _id { get; set; }
        public string TenantId { get; set; }
        public string Prefix { get; set; }
        public string TenantName { get; set; }
        public IList<DomainIdentifier> DomainIdentifiers { get; set; }
        public IList<ModuleMapped> ModuleMapped { get; set; } 
        public string ConnectionString { get; set; }
        public string AuthType { get; set; }
        public string DBCollectionName { get; set; }
        public bool IsActive { get; set; }
        public string UpdatedBy { get; set; }
        public string UpdatedDate { get; set; }
        public TenantConnectionType TenantType { get; set; }
        public string AzureVaultConnectionKey { get; set; }
    }
    public class DomainIdentifier
    {
        public string Id { get; set; }
        public string Name { get; set; }

    }
    public class ModuleMapped
    {
        public string Id { get; set; }
        public string Name { get; set; }

    }
    public enum TenantConnectionType
    {
        Single,
        Multi
    }
}
