namespace SearchLego.DataSeeder.Azure.Constant
{
    public class AppSettings
    {
        public ConnectionString ConnectionString { get; set; }
    }
}

public class ConnectionString
{
    public string ClientId { get; set; }
    public string TenantId { get; set; }

    public string ClientSecret { get; set; }
    public string KeyVaultName { get; set; }
}