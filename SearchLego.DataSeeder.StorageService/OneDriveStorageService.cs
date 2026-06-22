using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using Newtonsoft.Json;
using SearchLego.DataSeeder.Common;
namespace SearchLego.DataSeeder.StorageService
{
    public class OneDriveStorageService : IStorageService
    {
        private OneDriveStorageConfig storageConfig;
        private OneDriveDataService OneDrive;
        public OneDriveStorageService(OneDriveStorageConfig storageProviderConfig)
        {
            storageConfig = storageProviderConfig;
            OneDrive = new OneDriveDataService(storageConfig);
        }
        public List<FileDescription> GetFiles(string prefixOrFolderName, DateTime lastModified)
        {
            return OneDrive.getFiles("Documents", prefixOrFolderName);
        }

        public Stream GetFileStream(string fileName)
        {
            var stream = OneDrive.getFile(fileName);
            return stream;
            //throw new NotImplementedException();
        }
    }
}
