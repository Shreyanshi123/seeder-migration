using Newtonsoft.Json;
using SearchLego.DataSeeder.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace SearchLego.DataSeeder.StorageService
{
    public class OneDriveDataService
    {
        //private const string library = "Documents";
        //private const string userName = "ranjeet.tiwari@evalueserve.com";
        //private const string password = "Sept@092022$$$$";
        //private readonly SecureString secureString = null;

        public OneDriveDataService(OneDriveStorageConfig config)
        {
            //secureString = new SecureString();
            //password.ToCharArray().All(x =>
            //{
            //    secureString.AppendChar(x);
            //    return true;
            //});
        }

        public List<FileDescription> getFiles(string library, string prefixOrFolderName = "")
        {
            List<FileDescription> files = new List<FileDescription>();
            var fileData = CallApi(library, "http://localhost:52273/", "/api/values/filelist", "application/json").getStringResult();
            var result = JsonConvert.DeserializeObject<List<SharePointData>>(fileData);
            result.Where(x => x.FileName.StartsWith(prefixOrFolderName, StringComparison.OrdinalIgnoreCase)).All(x =>
            {
                files.Add(new FileDescription
                {
                    id = x.id,
                    FileName = x.FileName,
                    FileSize = x.FileSize,
                    FolderName = x.FolderName,
                    LastModified = x.LastModified,
                    Url = x.FilePath
                });
                return true;
            });
            return files;
        }

        public Stream getFile(string fileUrl)
        {
            return CallApi(fileUrl, "http://localhost:52273/", "/api/values/filebytes", "application/json").getFileStreamResult();
        }

        public HttpResponseMessage CallApi(object data, string baseUrl, string requestUri, string mediaType)
        {
            HttpResponseMessage response = new HttpResponseMessage();
            try
            {
                using (var client = new HttpClient())
                {

                    client.Timeout = TimeSpan.FromSeconds(200);
                    client.MaxResponseContentBufferSize = 2147483647;
                    // Setting Base address.  
                    client.BaseAddress = new Uri(baseUrl);
                    // Setting content type.  
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
                    // Initialization.  
                    response = client.PostAsJsonAsync(requestUri, data).Result;
                }
                return response;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

}
