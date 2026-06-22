using System;
using System.Collections.Generic;
using System.Text;

namespace SearchLego.DataSeeder.StorageService
{
    public class OneDriveStorageConfig
    {
        public string URL { get; set; }
    }
    public class SharePointData
    {
        public string id { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string FolderName { get; set; }
        public DateTime LastModified { get; set; }
        public string CreatedBy { get; set; }

    }
}
