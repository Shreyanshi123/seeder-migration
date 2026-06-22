using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SearchLego.DataSeeder.StorageService
{
    public interface IStorageService
    {
        public List<FileDescription> GetFiles(string prefixOrFolderName, DateTime lastModified);
        public Stream GetFileStream(string fileName);
    }
}
