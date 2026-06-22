using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace SearchLego.DataSeeder.NER
{
    public class NERProcess : INERProcess, IDisposable
    {
        public string NERDataProcess(object data, string baseUrl, string url)
        {
            string result = string.Empty;
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(100000);
                    //client.MaxResponseContentBufferSize = 2147483647;
                    // Setting Base address.  
                    client.BaseAddress = new Uri(baseUrl);
                    // Setting content type.  
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    // Initialization.  
                    var response = client.PostAsJsonAsync(url, data).Result;
                    // Verification  
                    if (response.IsSuccessStatusCode)
                        result = response.Content.ReadAsStringAsync().Result;
                }
                return result;
            }
            catch (Exception ex)
                {
                
                // Create a new file     
                //string fileName = @"D:\Exceptions\"+ DateTime.Now.Ticks.ToString() + ".json";
                //using (FileStream fs = File.Create(fileName))
                //{
                //    // Add some text to file    

                //    byte[] author = new UTF8Encoding(true).GetBytes(JsonConvert.SerializeObject(data));
                //    fs.Write(author, 0, author.Length);
                //}

                throw ex;
            }

        }

        public string DataDictionaryProcessRequest(object data, string baseURl)
        {
            string result = string.Empty;
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10000);
                    // Setting Base address.  
                    client.BaseAddress = new Uri(baseURl);
                    // Setting content type.  
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    // Initialization.  
                    var response = client.PostAsJsonAsync("cognitive/data_dictionaries/dictionary", data).Result;
                    // Verification  
                    if (response.IsSuccessStatusCode)
                        result = response.Content.ReadAsStringAsync().Result;
                }
                return result;
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}



