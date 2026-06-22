using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SearchLego.DataSeeder.Connector
{
    public class TenantData
    {

        private readonly ILogger<dynamic> _logger;
        private readonly ITenantService _tenantService;
        public TenantData(ILogger<dynamic> logger, ITenantService tenantService)
        {
            _logger = logger;
            _tenantService = tenantService;
        }
        public IList<Tenant> TenantDataPrepare(string jobId, TenantConnectionType tenantConnectionType, ApplicationConfig applicationConfig)
        {
            IList<Tenant> tenantList = new List<Tenant>();

            try
            {
                var getAllTenantlist = _tenantService.GetAllTenant(tenantConnectionType, applicationConfig).Result;
                if (getAllTenantlist != null)
                {
                    foreach (var tenant in getAllTenantlist)
                    {
                        tenantList.Add(tenant);
                    }
                    if (tenantList.Any())
                        ApplicationConfig.TenantCollections = tenantList;

                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Initial_Validation - job id : {jobId}, Tenant exception, {ex.Message}, {ex.StackTrace}");
            }
            return tenantList;
        }
    }
}
