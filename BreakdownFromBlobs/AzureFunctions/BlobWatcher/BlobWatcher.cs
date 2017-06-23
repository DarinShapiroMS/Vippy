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
using Microsoft.WindowsAzure.Storage;

namespace BreakdownFromBlobs
{
    public static class BlobWatcher
    {

        private static TraceWriter _log;

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
            log.Info($"Blob named {fileName} being procesed by BlobWatcher function..");

            // get a SAS url for the blob       
            string SaSUrl = GetSasUrl(myBlob);
            log.Info($"Got SAS url {SaSUrl}");

            // call the api to process the video in VideoIndexer
            var VideoIndexerUniqueId = SubmitToVideoIndexerAsync(SaSUrl);
            _log.Info($"VideoId {VideoIndexerUniqueId} submitted to Video Indexer!");


            // TODO: put in a database to track current jobs
            await StoreProcessingStateRecordInCosmosAsync(fileName, VideoIndexerUniqueId.Result);

        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="SaSUrl">Secure link to video file in Azure Storage</param>
        /// <returns>VideoBreakdown in JSON format</returns>
        private static async System.Threading.Tasks.Task<string> SubmitToVideoIndexerAsync(string SaSUrl)
        {

            string Video_Indexer_Callback_url = ConfigurationManager.AppSettings["Video_Indexer_Callback_url"];
            if (String.IsNullOrEmpty(Video_Indexer_Callback_url))
                throw new ApplicationException("Video_Indexer_Callback_url app setting not set");


            var queryString = HttpUtility.ParseQueryString(string.Empty);
            // These can be used to set meta data visible in the VI portal.  
            // required settings
            queryString["videoUrl"] = SaSUrl;
            queryString["language"] = "English";
            queryString["privacy"] = "private";

            // optional settings - mostly VI portal UI related
            queryString["name"] = "Fashion Video";
            queryString["description"] = "A video of a fashion model";
            queryString["callbackUrl"] = Video_Indexer_Callback_url;
            //queryString["externalId"] = "{string}";   // Use this to track AMS Asset ID, or even unqique customer ID for the video
            //queryString["metadata"] = "{string}";
            //queryString["partition"] = "{string}";

       
            var apiUrl = Globals.VideoIndexerApiUrl;
            var client = Globals.GetVideoIndexerHttpClient();

            // post to the API
            var result = await client.PostAsync(apiUrl + $"?{queryString}", null);

            // the JSON result in this case is the VideoIndexer assigned ID for this video.
            var json = result.Content.ReadAsStringAsync().Result;
            
            
            //var VideoIndexerId = JsonConvert.DeserializeObject<string>(json);

            

            // delete the breakdown
            //DeleteBreakdown();
            // don't delete for now since we need to use the VI portal to train faces

            return json;
        }


        /// <summary>
        /// Inserts a receipt like record in the database. This record will be updated when the processing
        /// is completed with success or error details
        /// </summary>
        /// <param name="videoIndexerId"></param>
        /// <returns></returns>
        private static async System.Threading.Tasks.Task StoreProcessingStateRecordInCosmosAsync(string blobName, string videoIndexerId)
        {
            var collectionName = "VIProcessingState";
            var client = Globals.GetCosmosClient(collectionName);

            var state = new VIProcessingStatePOCO()
            {
                BlobName = blobName,
                VIUniqueId = videoIndexerId,
                StartTime = DateTime.Now,
                EndTime = null,
                ErrorMessage = ""
            };            
          
            // save the json as a new document
            Document r = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(Globals.CosmosDatabasename, collectionName), state);

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