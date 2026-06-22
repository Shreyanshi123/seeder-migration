using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using LiteX.Storage.FileSystem;
namespace SearchLego.DataSeeder.StorageService
{
    internal class FileNetworkStorageService : IStorageService
    {
        private FileSystemStorageConfig storageConfig;
        //private FileSystemStorageService filestorage;
        public FileNetworkStorageService(FileSystemStorageConfig storageProviderConfig)
        {
            storageConfig = storageProviderConfig;
            //filestorage = new FileSystemStorageService(storageProviderConfig, ;
        }

        public List<FileDescription> GetFiles(string prefixOrFolderName, DateTime lastModified)
        {
            List<FileDescription> files = new List<FileDescription>();
            var networkPath = Path.Join(storageConfig.Directory, "");

            files = Directory.GetFiles(networkPath, "*", SearchOption.AllDirectories).Where(file => file.StartsWith(prefixOrFolderName,StringComparison.OrdinalIgnoreCase)).Select(filePath =>
            {
                var fileId = Guid.NewGuid();
                var fileNameWithExt = Path.GetFileName(filePath);
                var fileExtension = Path.GetExtension(fileNameWithExt).Replace(".", "");
                var fileSize = new System.IO.FileInfo(filePath).Length;
                //var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithExt);
                string filePathWithoutNetworkPath = filePath.Substring(storageConfig.Directory.Length);
                string folderName = filePathWithoutNetworkPath.Replace(fileNameWithExt, "");
                var lastModified = System.IO.File.GetLastWriteTime(filePath);
                return new FileDescription
                {
                    id = fileId.ToString(),
                    Url = filePath,
                    FileName = filePath,
                    FolderName = folderName,
                    FileSize = fileSize,
                    //FileExtension = fileExtension,
                    LastModified = lastModified,
                };
            }).ToList();

            return files;
        }

        public Stream GetFileStream(string fileName)
        {
            return new FileStream(fileName, FileMode.Open, FileAccess.Read);
        }
    }
}
