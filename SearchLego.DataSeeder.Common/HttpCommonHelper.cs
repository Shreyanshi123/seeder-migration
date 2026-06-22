using System.Net.Http;
using System.IO;

namespace SearchLego.DataSeeder.Common
{
    public static class HttpHelper
    {
        public static string getStringResult(this HttpResponseMessage response)
        {
            if (response != null && response.IsSuccessStatusCode)
                return response.Content.ReadAsStringAsync().Result;
            else
                return string.Empty;
        }

        public static Stream getFileStreamResult(this HttpResponseMessage response)
        {
            if (response != null && response.IsSuccessStatusCode)
                return response.Content.ReadAsStreamAsync().Result;
            else
                return null;
        }
    }
}
