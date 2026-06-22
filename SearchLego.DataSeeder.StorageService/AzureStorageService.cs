using System;
using System.Collections.Generic;
using System.Text;
using LiteX.Storage.Azure;
using System.Linq;
using System.IO;
using Azure.Storage.Blobs;
using LiteX.Storage.Core;

namespace SearchLego.DataSeeder.StorageService
{
    internal class AzureStorageService : IStorageService
    {
        private AzureBlobStorageConfig storageConfig;
        private AzureBlobStorageService azure;
        public AzureStorageService(AzureBlobStorageConfig storageProviderConfig)
        {
            storageConfig = storageProviderConfig;
            azure = new AzureBlobStorageService(storageConfig);
        }

        public List<FileDescription> GetFiles(string prefixOrFolderName, DateTime lastModified)
        {
            List<FileDescription> files = new List<FileDescription>();
            BlobContainerClient container = new BlobContainerClient(storageConfig.AzureBlobStorageConnectionString, storageConfig.AzureBlobStorageContainerName);
            DateTime _lastModifiedDate = new DateTime(1970, 1, 1);
            _lastModifiedDate = lastModified < _lastModifiedDate ? _lastModifiedDate : lastModified;
            var descriptor = container.GetBlobs().Where(m => (m.Properties.LastModified > _lastModifiedDate) &&
                                            (m.Name.StartsWith(prefixOrFolderName, StringComparison.OrdinalIgnoreCase))).
                                            Select(x => new BlobDescriptor
                                            {
                                                Name = x.Name,
                                                Url = container.Uri.AbsoluteUri + "/" + x.Name,
                                                Length = x.Properties.ContentLength.Value,
                                                LastModified = x.Properties.LastModified.Value,
                                            }).ToList();

            //var blobs = azure.GetBlobsAsync(storageConfig.AzureBlobStorageContainerName).GetAwaiter().GetResult();

            //var descriptor = blobs.Where(x => x.Name.StartsWith(prefixOrFolderName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (descriptor != null && descriptor.Any())
            {
                files = descriptor.Select(x =>
                {
                    string name = x.Name ?? string.Empty;
                    prefixOrFolderName = name.Length > prefixOrFolderName.Length ? name.Substring(prefixOrFolderName.Length) : prefixOrFolderName;
                    prefixOrFolderName = !string.IsNullOrEmpty(prefixOrFolderName) ? prefixOrFolderName : "NA";
                    return FileDescription.fromBlobDescriptor(x, prefixOrFolderName);
                }).ToList();
                //files = descriptor.Select(x => FileDescription.fromBlobDescriptor(x, prefixOrFolderName)).ToList();
            }
            return files;
        }

        public Stream GetFileStream(string fileName)
        {
            return azure.GetBlobAsync(fileName).GetAwaiter().GetResult();
        }
    }
}
