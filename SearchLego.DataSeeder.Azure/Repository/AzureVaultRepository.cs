using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchLego.DataSeeder.Azure.Constant;
using System;

namespace SearchLego.DataSeeder.Azure
{
    public class AzureVaultRepository : IAzureVaultRepository
    {
        private readonly ILogger<dynamic> logger;

        public AzureVaultRepository(ILogger<dynamic> _logger = null)
        {
            logger = _logger ?? new Logger<dynamic>(new LoggerFactory());
        }

        public string GetSecretValue(string secretName)
        {
            try
            {
                logger.LogInformation($"Getting Secret for {secretName} from Azure Vault");
                var connectionString = GetConnectionString();
                string clientId = connectionString.ClientId;
                string clientSecret = connectionString.ClientSecret;
                string tenantId = connectionString.TenantId;
                var keyVaultName = connectionString.KeyVaultName;
                var kvUri = $"https://{keyVaultName}.vault.azure.net";

                var kvClient = new SecretClient(vaultUri: new Uri(kvUri), credential: new ClientSecretCredential(tenantId, clientId, clientSecret));
                var fetchedSecret = kvClient.GetSecret(secretName);
                var secretValue = fetchedSecret?.Value;
                return secretValue?.Value;
            }

            catch (Exception ex)
            {
                logger.LogError($"Error in fetching secret {secretName} from Azure Vault, {ex.Message}, {ex.StackTrace}");
            }

            return null;
        }

        private ConnectionString GetConnectionString()
        {
            try
            {
                var builder = new ConfigurationBuilder().SetBasePath(System.AppContext.BaseDirectory)
                 .AddJsonFile("Config/azureAppSettings.json", optional: false, reloadOnChange: true);
                IConfigurationRoot configuration = builder.Build();

                var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>();
                return appSettings.ConnectionString;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in getting connection from Azure app settings, {ex.Message}, {ex.StackTrace}");
            }

            return new ConnectionString();
        }
    }
}
