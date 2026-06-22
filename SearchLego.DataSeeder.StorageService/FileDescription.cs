using LiteX.Storage.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SearchLego.DataSeeder.StorageService
{
    public class FileDescription
    {
        public string id { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public DateTime LastModified { get; set; }
        public string FileExtension
        {
            get
            {
                return Path.GetExtension(this.FileName ?? "").Replace(".", "");
            }
        }
        public long FileSize { get; set; }
        public string FolderName { get; set; }

        public static FileDescription fromBlobDescriptor(BlobDescriptor descriptor, string prefixOrFolderName = "")
        {
            var fileId = Guid.NewGuid();
            return new FileDescription
            {
                id = fileId.ToString(),
                FileName = descriptor.Name,
                Url = descriptor.Url,
                FolderName = prefixOrFolderName,
                FileSize = descriptor.Length,
                LastModified = descriptor.LastModified.HasValue ? descriptor.LastModified.Value.DateTime : DateTime.Now,
            };
        }
    }
}
