using Newtonsoft.Json;
using OrchestrationFunctions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SubmitToVideoIndexer
{
    class Program
    {
        private static string key = "cb2980a155f0465c9cc5fed037f4596f";
        static  void Main(string[] args)
        {
            //UploadVideo();
            //GetBreakdown("52a530d4cc");
            //var stream = GetImageStream("https://www.videoindexer.ai/api/Thumbnail/be7041c80e/9e8ca8df-e9e1-48d7-be77-ac3a958cea71");
            //LabelFaceAsync("52a530d4cc", "1164", "Cindy Lopez");

            // json testing
            StreamReader sr = new StreamReader(new FileStream(@"C:\Users\dashapir\Documents\visual studio 2017\Projects\VideoIndexerPOC\SampleBreakdown.json", FileMode.Open));
            var json = sr.ReadToEnd();

            VideoBreakdownPOCO oMyclass = Newtonsoft.Json.JsonConvert.DeserializeObject<VideoBreakdownPOCO>(json);

            Console.WriteLine("SDFSDF");
        }

        private static void LabelFaceAsync(string VideoId, string FaceId, string NewName)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp -Apim-Subscription-Key", $"{key}");

            // Request parameters
            queryString["faceId"] = $"{FaceId}";
            queryString["newName"] = $"{NewName}";
            var uri = $"https://videobreakdown.azure-api.net/Breakdowns/Api/Partner/Breakdowns/UpdateFaceName/{VideoId}?" + queryString;

            HttpResponseMessage response;

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes("{body}");

            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response =  client.PutAsync(uri, content).Result;
            }

        }

        private static void GetBreakdown(string Id)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);

            // Request parameters
            queryString["language"] = "English";
            var uri = $"https://videobreakdown.azure-api.net/Breakdowns/Api/Partner/Breakdowns/{Id}?" + queryString;

            var response = client.GetAsync(uri).Result;

           
            var json = response.Content.ReadAsStringAsync().Result;
            Console.Write(json);
            Console.ReadLine();
        }

        private static void UploadVideo()
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            // Request parameters
            queryString["name"] = "Fashion Video";
            queryString["privacy"] = "private";
            //queryString["videoUrl"] = "{string}";
            queryString["language"] = "English";
            //queryString["externalId"] = "{string}";
            //queryString["metadata"] = "{string}";
            queryString["description"] = "A video of a fashion model";
            //queryString["partition"] = "{string}";

            var apiUrl = "https://videobreakdown.azure-api.net/Breakdowns/Api/Partner/Breakdowns";
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
       
            var videoPath = @"C:\Users\dashapir\Downloads\Fashion_DivX720p_ASP.divx";  // works
            //var videoPath = @"C:\Users\dashapir\Downloads\AmosTV_10min_HT.divx";       // fails
            //var videoPath = @"C:\Users\dashapir\Downloads\Helicopter_DivXHT_ASP.divx";   // works
              var content = new MultipartFormDataContent
            {
                { new StreamContent(File.Open(videoPath, FileMode.Open)), "Video", "Video" }
            };
            Console.WriteLine("Uploading...");
            var result = client.PostAsync(apiUrl + $"?{queryString}", content).Result;
            var json = result.Content.ReadAsStringAsync().Result;

            Console.WriteLine();
            Console.WriteLine("Uploaded:");
            Console.WriteLine(json);

            var id = JsonConvert.DeserializeObject<string>(json);

            while (true)
            {
                Thread.Sleep(10000);

                result = client.GetAsync(string.Format(apiUrl + "/{0}/State", id)).Result;
                json = result.Content.ReadAsStringAsync().Result;

                Console.WriteLine();
                Console.WriteLine("State:");
                Console.WriteLine(json);

                dynamic state = JsonConvert.DeserializeObject(json);
                if (state.state != "Uploaded" && state.state != "Processing")
                {
                    break;
                }
            }

            result = client.GetAsync(string.Format(apiUrl + "/{0}", id)).Result;
            json = result.Content.ReadAsStringAsync().Result;
            Console.WriteLine();
            Console.WriteLine("Full JSON:");
            Console.WriteLine(json);

            result = client.GetAsync(string.Format(apiUrl + "/Search?id={0}", id)).Result;
            json = result.Content.ReadAsStringAsync().Result;
            Console.WriteLine();
            Console.WriteLine("Search:");
            Console.WriteLine(json);

            result = client.GetAsync(string.Format(apiUrl + "/{0}/InsightsWidgetUrl", id)).Result;
            json = result.Content.ReadAsStringAsync().Result;
            Console.WriteLine();
            Console.WriteLine("Insights Widget url:");
            Console.WriteLine(json);

            result = client.GetAsync(string.Format(apiUrl + "/{0}/PlayerWidgetUrl", id)).Result;
            json = result.Content.ReadAsStringAsync().Result;
            Console.WriteLine();
            Console.WriteLine("Player token:");
            Console.WriteLine(json);

            Console.ReadLine();
        }

        private static byte[] GetImageStream(string Url)
        {
            var client = new WebClient();
            var imageBytes = client.DownloadData(Url);
            return imageBytes;
        }

        /// <summary>
        /// Stores the video breakdown data in Cosmos DB
        /// </summary>
        private static void PersistBreakdown() { }
    }
}
