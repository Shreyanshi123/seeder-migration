using System;
using System.Collections.Generic;
using System.Text;
using LiteX.Storage.AmazonS3;
using LiteX.Storage.Azure;
using LiteX.Storage.Core;
using LiteX.Storage.FileSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SearchLego.DataSeeder.StorageService
{
    public class StorageProvider
    {
        StorageTypes storageType;

        public StorageProvider(StorageTypes _storageType)
        {
            this.storageType = _storageType;
        }

        private AzureBlobStorageConfig toAzureConfig(dynamic providerConfig)
        {

            AzureBlobStorageConfig blobConfig = new AzureBlobStorageConfig();
            if(providerConfig != null)
            {
               blobConfig = JsonConvert.DeserializeObject<AzureBlobStorageConfig>(JsonConvert.SerializeObject(providerConfig, new JsonSerializerSettings
               {
                   ContractResolver = new CamelCasePropertyNamesContractResolver()
               }));
            }
            return blobConfig;
        }

        private AmazonS3Config toAmazonConfig(dynamic providerConfig)
        {
            AmazonS3Config s3config = new AmazonS3Config();
            if (providerConfig != null)
            {
                s3config = JsonConvert.DeserializeObject<AmazonS3Config>(JsonConvert.SerializeObject(providerConfig, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                }));
            }
            return s3config;
        }

        private FileSystemStorageConfig toFileSystemConfig(object config)
        {
            FileSystemStorageConfig fileconfig = new FileSystemStorageConfig();
            if (config != null)
            {
                fileconfig = JsonConvert.DeserializeObject<FileSystemStorageConfig>(JsonConvert.SerializeObject(config, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                }));
            }
            return fileconfig;
        }

        private OneDriveStorageConfig toOneDriveConfig(object config)
        {
            OneDriveStorageConfig fileconfig = new OneDriveStorageConfig();
            if (config != null)
            {
                fileconfig = JsonConvert.DeserializeObject<OneDriveStorageConfig>(JsonConvert.SerializeObject(config, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                }));
            }
            return fileconfig;
        }
        public IStorageService StorageService(string configJSON)
        {
            object obj = JsonConvert.DeserializeObject(configJSON);
            switch (this.storageType)
            {
                case StorageTypes.Azure: return new AzureStorageService(toAzureConfig(obj));
                case StorageTypes.AWS: return new AWSS3StorageService(toAmazonConfig(obj));
                case StorageTypes.OneDrive: return new OneDriveStorageService(toOneDriveConfig(obj));

                case StorageTypes.Google:
                    break;
                case StorageTypes.FileSystem: return new FileNetworkStorageService(toFileSystemConfig(obj));
                case StorageTypes.Kvpbase:
                    break;
                case StorageTypes.Other:
                    break;
                default:
                    break;
            }
            return null;
        }

    }
}
