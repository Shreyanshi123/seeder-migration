using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Nest;
using SearchLego.DataSeeder.Azure;
using SearchLego.DataSeeder.Common;
using System;

namespace SearchLego.DataSeeder.Elastic
{
    public class ElasticConnector : IElasticConnector
    {
        private const string DefaultIndex = "searchlego";
        public ElasticClient _elasticClient { get; set; }
        public ElasticLowLevelClient _lowLevelClient { get; set; }
        private static string _uri;
        private static string _userName;
        private static string _password;
        private IAzureVaultRepository azureVaultRepository;
        private ApplicationConfig _configDetail;

        public ElasticConnector(ILogger<dynamic> logger, ApplicationConfig configDetail)
        {
            _configDetail = configDetail;

            if (string.IsNullOrEmpty(_uri))
            {
                if (_configDetail.IsAzureVaultEnabled)
                {
                    azureVaultRepository = new AzureVaultRepository(logger);
                    _uri = azureVaultRepository.GetSecretValue(_configDetail.AzureVaultKeys?.ElasticUri);
                }
                else
                {
                    _uri = _configDetail.ElasticUri;
                }
            }
            if(_configDetail.UseElasticCloud && (string.IsNullOrEmpty(_userName) || string.IsNullOrEmpty(_password)))
            {
                if (_configDetail.IsAzureVaultEnabled)
                {
                    azureVaultRepository = new AzureVaultRepository(logger);
                    _userName = azureVaultRepository.GetSecretValue(_configDetail.AzureVaultKeys?.ElasticUserName);
                    _password = azureVaultRepository.GetSecretValue(_configDetail.AzureVaultKeys?.ElasticPassword);
                }
                else
                {
                    _userName = _configDetail.ElasticUserName;
                    _password = _configDetail.ElasticPassword;
                }
            }
        }

        public void ElasticConnect(string indexName=null)
        {

            ConnectionSettings settings;
            var nodes = new Uri[]
                {
                    new Uri(_uri),
                };

            var connectionPool = new StaticConnectionPool(nodes);
            settings = new ConnectionSettings(connectionPool);


            var elasticUri = new Uri(_uri);
            settings = new ConnectionSettings(elasticUri);

            //authenticate for elastic cloud
            if (_configDetail.UseElasticCloud)
            {
                settings.BasicAuthentication(_userName, _password);
            }

            settings = settings.DefaultIndex(indexName ?? DefaultIndex);

            _elasticClient = new ElasticClient(settings);

            _lowLevelClient = new ElasticLowLevelClient(settings);
        }
    }

    public interface IElasticConnector
    {
        ElasticClient _elasticClient { get; set; }
        ElasticLowLevelClient _lowLevelClient { get; set; }
        void ElasticConnect(string indexName=null);
    }
}
