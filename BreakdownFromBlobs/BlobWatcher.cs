using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Net.Http;
using System.Web;
using System.Configuration;
using Newtonsoft.Json;
using System.Threading;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using OrchestrationFunctions;

namespace BreakdownFromBlobs
{
    public static class BlobWatcher
    {

        private static TraceWriter _log;
        private const string EndpointUrl = "<your endpoint URL>";
        private const string PrimaryKey = "<your primary key>";
        private static DocumentClient client;

        [FunctionName("BlobWatcher")]
        public static async System.Threading.Tasks.Task RunAsync([BlobTrigger("video-input/{name}", Connection = "AzureWebJobsStorage")] CloudBlockBlob myBlob,
            string name,
            TraceWriter log

            )
        {

            // =============================================================================================
            // This function watches a blob container and does the following for each video it finds
            //      1. Submits the video to Video Indexer to be processed.
            //      2. Monitors progress of the indexing job
            //      3. When complete, it takes the output - the video breakdown and stores it in Cosmos
            //
            // =============================================================================================

            _log = log;

            // TODO: validate file types here or add file extension filters to blob trigger

            // blob filename
            string fileName = myBlob.Name;

            // get a SAS url for the blob       
            string SaSUrl = GetSasUrl(myBlob);

            // call the api to process the video in VideoIndexer
            var videoBreakdownJson = SubmitToVideoIndexer(SaSUrl);

            // stuff the json into Cosmos
            await SaveInCosmosAsync(videoBreakdownJson);

            
        }

        private static async System.Threading.Tasks.Task SaveInCosmosAsync(VideoBreakdownPOCO videoBreakdownJson)
        {
            string endpoint = ConfigurationManager.AppSettings["cosmos_enpoint"];
            if (String.IsNullOrEmpty(endpoint))
                throw new ApplicationException("cosmos_enpoint app setting not set");

            string key = ConfigurationManager.AppSettings["cosmos_key"];
            if (String.IsNullOrEmpty(key))
                throw new ApplicationException("cosmos_key app setting not set");

            client = new DocumentClient(new Uri(endpoint), key);
            
            Document r = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri("VideoBreakdowns", "breakdowns2"), videoBreakdownJson);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="SaSUrl">Secure link to video file in Azure Storage</param>
        /// <returns>VideoBreakdown in JSON format</returns>
        private static VideoBreakdownPOCO SubmitToVideoIndexer(string SaSUrl)
        {

            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // These can be used to set meta data visible in the VI portal.  
            // required settings
            queryString["videoUrl"] = SaSUrl;
            queryString["language"] = "English";
            queryString["privacy"] = "private";

            // optional settings - mostly VI portal UI related
            queryString["name"] = "Fashion Video";
            queryString["description"] = "A video of a fashion model";
            //queryString["externalId"] = "{string}";
            //queryString["metadata"] = "{string}";
            //queryString["partition"] = "{string}";

            // Video Indexer API key stored in settings (App Settings in Azure Function portal)
            string VideoIndexerKey = ConfigurationManager.AppSettings["video_indexer_key"];
            if (String.IsNullOrEmpty(VideoIndexerKey))
                throw new ApplicationException("VideoIndexerKey app setting not set");

            // Video Indexer API Url
            var apiUrl = "https://videobreakdown.azure-api.net/Breakdowns/Api/Partner/Breakdowns";
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", VideoIndexerKey);

            // post to the API
            var result = client.PostAsync(apiUrl + $"?{queryString}", null).Result;

            // the JSON result in this case is the VideoIndexer assigned ID for this video.
            var json = result.Content.ReadAsStringAsync().Result;
            var VideoIndexerId = JsonConvert.DeserializeObject<string>(json);

            // monitor progress of indexing operation
            while (true)
            {
                Thread.Sleep(10000);

                result = client.GetAsync(string.Format(apiUrl + "/{0}/State", VideoIndexerId)).Result;
                json = result.Content.ReadAsStringAsync().Result;

                _log.Info($"VideoId {VideoIndexerId} Processing State: {json}");

                dynamic state = JsonConvert.DeserializeObject(json);
                if (state.state != "Uploaded" && state.state != "Processing")
                {
                    break;
                }
            }

            // operation has completed, get the full JSON of the video breakdown
            result = client.GetAsync(string.Format(apiUrl + "/{0}", VideoIndexerId)).Result;
            json = result.Content.ReadAsStringAsync().Result;



            _log.Info($"VideoId {VideoIndexerId} completed processing, {json.Length} bytes returned in JSON response");


            // delete the breakdown
            //DeleteBreakdown();
            // don't delete for now since we need to use the VI portal to train faces

            return Newtonsoft.Json.JsonConvert.DeserializeObject<VideoBreakdownPOCO>(json); ;
        }

        /// <summary>
        /// Gets a URL with a SAS token that is good to read the file for 1 hour
        /// </summary>
        /// <param name="myBlob"></param>
        /// <returns></returns>
        private static string GetSasUrl(CloudBlockBlob myBlob)
        {
            // expiry time set 5 minutes in the past to 1 hour in the future. THis can be
            // moved into configuration if needed
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
            sasConstraints.SharedAccessStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
            sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(1);
            sasConstraints.Permissions = SharedAccessBlobPermissions.Read;

            //Generate the shared access signature on the blob, setting the constraints directly on the signature.
            string sasBlobToken = myBlob.GetSharedAccessSignature(sasConstraints);
            return myBlob.Uri + sasBlobToken;
        }
    }
}