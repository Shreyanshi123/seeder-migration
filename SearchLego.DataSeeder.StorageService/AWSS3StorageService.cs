using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LiteX.Storage.AmazonS3;
namespace SearchLego.DataSeeder.StorageService
{
    public class AWSS3StorageService : IStorageService
    {
        private AmazonS3Config storageConfig;
        private AmazonS3Service aws;
        public AWSS3StorageService(AmazonS3Config storageProviderConfig)
        {
            storageConfig = storageProviderConfig;
            aws = new AmazonS3Service(storageConfig);
        }

        public List<FileDescription> GetFiles(string prefixOrFolderName, DateTime lastModified)
        {
            List<FileDescription> files = new List<FileDescription>();
            var blobs = aws.GetBlobsAsync(storageConfig.AmazonBucketName).GetAwaiter().GetResult();
            var descriptor = blobs.Where(x => !x.ContentType.Contains("x-directory", StringComparison.OrdinalIgnoreCase) && x.Name.StartsWith(prefixOrFolderName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (descriptor != null && descriptor.Any())
            {
                files = descriptor.Select(x => FileDescription.fromBlobDescriptor(x, prefixOrFolderName)).ToList();
            }
            return files;
        }

        // do fs.close() whereever you call this method
        public Stream GetFileStream(string fileName)
        {
            return aws.GetBlobAsync(fileName).GetAwaiter().GetResult();
        }
    }
}