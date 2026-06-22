using LiteX.Storage.Core;
using System.IO;

namespace SearchLego.DataSeeder.CloudStorage
{
    public class FileFromCloudStorage
    {
        private readonly ILiteXStorageProviderFactory _factory;
        public FileFromCloudStorage(ILiteXStorageProviderFactory factory)
        {
            _factory = factory;
        }
        public Stream GetStreamFromCloud(string fileName, StorageProviderType storageProviderType)
        {
            var provider = GetCloudInstance(storageProviderType);
            //var  blobs =  provider.GetBlobsAsync().Result;
            // List<BlobDescriptor> blobs = provider.GetBlobs().ToList();
            //foreach (var blob in blobs)
            //{
            return provider.GetBlob(fileName);
            //CopyStream(blobStream, Path.Combine(destinationPath, blob.Name));
            // }
        }


        public bool UploadStreamToCloud(StorageProviderType storageProviderType, string fileName, Stream stream)
        {
            var provider = GetCloudInstance(storageProviderType);
            return provider.UploadBlob(fileName, stream);
        }

        private ILiteXBlobService GetCloudInstance(StorageProviderType storageProviderType)
        {
            ILiteXBlobService blobService = null;
            switch (storageProviderType)
            {
                case StorageProviderType.Azure:
                    blobService = _factory.GetStorageProvider("azure");
                    break;
                case StorageProviderType.Amazon:
                    blobService = _factory.GetStorageProvider("amazons3");
                    break;
                case StorageProviderType.Google:
                    blobService = _factory.GetStorageProvider("googlecloudstorage");
                    break;
                case StorageProviderType.FileSystem:
                    blobService = _factory.GetStorageProvider("filesystem");
                    break;
                case StorageProviderType.Kvpbase:
                    blobService = _factory.GetStorageProvider("Kvpbase");
                    break;
                case StorageProviderType.Other:
                    blobService = _factory.GetStorageProvider("other");
                    break;
            }

            return blobService;
        }

    }
}
