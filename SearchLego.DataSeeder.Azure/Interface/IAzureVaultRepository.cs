using System.Threading.Tasks;

namespace SearchLego.DataSeeder.Azure
{
    public interface IAzureVaultRepository
    {
        string GetSecretValue(string secretName);
    }
}
