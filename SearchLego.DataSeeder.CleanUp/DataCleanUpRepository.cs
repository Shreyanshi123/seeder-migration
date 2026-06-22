using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using SearchLego.DataSeeder.CleanUp;
using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace SearchLego.DataSeeder.Cleanup
{
    public class DataCleanUpRepository
    {
        public void CleanDocument(ILogger<dynamic> logger)
        {
            logger.LogInformation($"Cleanup job - Deletion from Mongo has been started");
            var appSetting = GetAppSettings(logger);
            var mongoClient = new MongoClient(appSetting.MongoConnectionString);
            var database = mongoClient.GetDatabase("doc_db");
            var collection = database.GetCollection<BsonDocument>("Documents");
            var query = Builders<BsonDocument>.Filter.Eq("IsActive", false);
            collection.DeleteMany(query);
            logger.LogInformation($"Cleanup job - Deletion from Mongo has been completed");

            logger.LogInformation($"Cleanup job - Deletion from File server has been started");
            DeleteFromDirectory(appSetting.TempPath, logger);
            DeleteFromDirectory(appSetting.TempDownloadPath, logger);
            logger.LogInformation($"Cleanup job - Deletion from File server has been completed");
        }

        private void DeleteFromDirectory(string path, ILogger<dynamic> logger)
        {
            try
            {
                var directory = new DirectoryInfo(path);
                if (directory.Exists)
                {
                    logger.LogInformation($"Cleanup job - Deletion of directory has been started");
                    var directoryInfos = directory.GetDirectories();
                    foreach (var directoryInfo in directoryInfos)
                    {
                        if (directoryInfo.CreationTime <= DateTime.Now.AddHours(-2))
                        {
                            directoryInfo.Delete(true);
                        }
                    }
                    logger.LogInformation($"Cleanup job - Deletion of directory has been completed");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Cleanup job - Deletion of directory - An error occurred {ex}");
            }
        }

        private AppSettings GetAppSettings(ILogger<dynamic> logger)
        {
            try
            {
                var builder = new ConfigurationBuilder().SetBasePath(System.AppContext.BaseDirectory)
                 .AddJsonFile("Config/cleanUpSetting.json", optional: false, reloadOnChange: true);
                IConfigurationRoot configuration = builder.Build();

                var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>();
                return appSettings;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in getting connection from Azure app settings, {ex.Message}, {ex.StackTrace}");
            }

            return new AppSettings();
        }
    }
}
