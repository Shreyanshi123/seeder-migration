using LiteX.Storage.AmazonS3;
using LiteX.Storage.Azure;
using LiteX.Storage.Core;
using LiteX.Storage.GoogleCloud;
using LiteX.Storage.Kvpbase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using SearchLego.DataSeeder.Azure;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Connector;
using SearchLego.DataSeeder.FileConvert;
using SearchLego.DataSeerer.Integration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SearchLego.DataSeeder.Schedular
{
    public static class ServicesConfiguration
    {
        /// <summary>
        /// This extention method is used for only add dependency injection in seperate manner  
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        public static void AddCustomServices(this IServiceCollection services, IConfigurationRoot configuration)
        {
            services.AddSingleton<IJobFactory, SingletonJobFactory>();
            services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
            services.AddSingleton<IUtilityFunctions, UtilityFunctions>();
            services.AddSingleton<IClientCrawlSetting, ClientCrawlSetting>();
            services.AddLogging(loggingBuilder => loggingBuilder
                      .AddConsole()
                      .AddDebug()
                      .SetMinimumLevel(LogLevel.Debug));
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog(@"Config\nlog.config");
            });
            services.AddSingleton<ISLBConfig, SLBConfig>();
            services.AddSingleton<ISettingValue, SettingValue>();
            services.AddSingleton<IExtractTextFromFile, ExtractTextFromFile>();
            services.AddSingleton<IConvertFileToPDF, ConvertFileToPDF>();
            services.AddSingleton<IAzureVaultRepository, AzureVaultRepository>();

            var congigDetail = configuration.GetSection("jobs").Get<IList<ApplicationConfig>>();
            var jobs = congigDetail?.Select(s => s).ToList().FirstOrDefault();

            if (jobs.DocumentPreviewSetting.Aspose.Enabled)
            {
                var objSlide = new ApsoseSlideLicense(jobs.DocumentPreviewSetting.Aspose.SlideLicPath);
                objSlide = null;
                var objWord = new ApsoseWordLicense(jobs.DocumentPreviewSetting.Aspose.WordLicPath);
                objWord = null;
                var objPdf = new ApsosePdfLicense(jobs.DocumentPreviewSetting.Aspose.PdfLicensePath);
                objPdf = null;
            }
            

            var azure = configuration.GetSection(CloudConstants.AZURE).Get<Common.Azure>();
            var amazon = configuration.GetSection(CloudConstants.AMAZON).Get<Common.Amazon>();
            var google = configuration.GetSection(CloudConstants.GOOGLE).Get<Common.Google>();
            var kvpbase = configuration.GetSection(CloudConstants.KVPBASE).Get<Kvpbase>();

            if (jobs.IsAzureVaultEnabled)
            {
                var azureRepository = new AzureVaultRepository();
                azure.AzureBlobStorageConfig.AzureBlobStorageConnectionString = GetAzureSecrets(azureRepository, azure.Enabled, jobs.AzureVaultKeys?.AzureBlobStorageConnectionString);
                amazon.AmazonS3Config.AmazonAwsSecretAccessKey = GetAzureSecrets(azureRepository, amazon.Enabled, jobs.AzureVaultKeys?.AmazonAwsSecretAccessKey);
                google.GoogleCloudStorageConfig.GoogleJsonAuthPath = GetAzureSecrets(azureRepository, google.Enabled, jobs.AzureVaultKeys?.GoogleJsonAuthPath);
                kvpbase.KvpbaseStorageConfig.KvpbaseApiKey = GetAzureSecrets(azureRepository, kvpbase.Enabled, jobs.AzureVaultKeys?.KvpbaseApiKey);
            }

            if (amazon.Enabled)
                services.AddLiteXAmazonS3Service(amazon.AmazonS3Config);
            if (azure.Enabled)
                services.AddLiteXAzureBlobStorageService(azure.AzureBlobStorageConfig);
            if (google.Enabled)
                services.AddLiteXGoogleCloudStorageService(google.GoogleCloudStorageConfig);
            if (kvpbase.Enabled)
                services.AddLiteXKvpbaseStorageService(kvpbase.KvpbaseStorageConfig);

            services.AddSingleton<ILiteXStorageProviderFactory, LiteXStorageProviderFactory>();
            services.AddSingleton<ITenantService, TenantService>();

            services.AddSingleton<IngestDataJob>();
            services.AddSingleton<PDFGenerateJob>();
            services.AddSingleton<CognitiveDataIngestionJob>();
            services.AddSingleton<CleanUpRepository>();
        }

        private static string GetAzureSecrets(AzureVaultRepository azureRepository, bool enabled, string secretName)
        {
            return enabled ? azureRepository.GetSecretValue(secretName) : "";
        }
    }
}
