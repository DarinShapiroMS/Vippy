using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Queue;
using System.IO;
using System.Net;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Net.Http;
using System.Web;
using System.Linq;

namespace OrchestrationFunctions
{
    public static class VideoIndexerCompleteQueueHandler
    {

        [FunctionName("VideoIndexerCompleteQueueHandler")]
        public static async Task RunAsync([QueueTrigger("vi-processing-complete", Connection = "AzureWebJobsStorage")]CloudQueueMessage myQueueItem, TraceWriter log)
        {
            string queueContents = myQueueItem.AsString;

            // queue item should be id & state
            Dictionary<string, string> completionData = JsonConvert.DeserializeObject<Dictionary<string, string>>(queueContents);

            // ignore if not proper state
            if (completionData["state"] != "Processed")
                return;

            var videoIndexerVideoId = completionData["id"];

            var apiUrl = Globals.VideoIndexerApiUrl;
            var client = Globals.GetVideoIndexerHttpClient();
            var result = client.GetAsync(string.Format(apiUrl + "/{0}", videoIndexerVideoId)).Result;
            var json = result.Content.ReadAsStringAsync().Result;

            var videoBreakdownPOCO = JsonConvert.DeserializeObject<VideoBreakdownPOCO>(json);

            // these tasks are network io dependant and can happen in parallel
            var englishCaptionsTask = GetCaptionsVttAsync(videoIndexerVideoId, videoBreakdownPOCO,"English");
            var japaneseCaptionsTask = GetCaptionsVttAsync(videoIndexerVideoId, videoBreakdownPOCO, "Japanese");
            var imagesTask = ExtractImages(videoBreakdownPOCO);
            await Task.WhenAll(englishCaptionsTask, japaneseCaptionsTask, imagesTask);


            await StoreBreakdownJsonInCosmos(videoBreakdownPOCO, log);
            await UpdateProcessingStateAsync(completionData["id"]);

        }

        private static async Task GetCaptionsVttAsync(string Id, VideoBreakdownPOCO videoBreakdownPOCO, string language)
        {

            var client = Globals.GetVideoIndexerHttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            
            // Request parameters
            queryString["language"] = language;
            var uri = $"https://videobreakdown.azure-api.net/Breakdowns/Api/Partner/Breakdowns/{Id}/VttUrl?" + queryString;

            // this returns a url to the captions file
            var response = await client.GetAsync(uri);
            var vttUrl = response.Content.ReadAsStringAsync().Result.Replace("\"", ""); // seems like the url is always wrapped in quotes

            // download actual vtt file and store in blob storage
            var vttStream = await DownloadWebResource(vttUrl);
            var blob = UploadFileToBlobStorage(vttStream, $"{videoBreakdownPOCO.id}/{language}.vtt", "text/plain");

            //TODO: put reference to vtt in breakdown?

        }

        /// <summary>
        /// Store the images referenced in the breakdown and update their location in the JSON prior to 
        /// storing in the database.
        /// </summary>
        /// <param name="poco"></param>
        /// <returns></returns>
        private static async Task ExtractImages(VideoBreakdownPOCO poco)
        {
            var pocoLock = new Object();

            // download thumbnail and store in blob storage
            var newImageUrl = "";

            var memSreamOfResource = await DownloadWebResource(poco.summarizedInsights.thumbnailUrl);
            var newBlob = await UploadFileToBlobStorage(memSreamOfResource, $"{poco.id}/video-thumbnail.jpg", "image/jpg");
                                     
            newImageUrl = newBlob.Uri.AbsoluteUri;

            // replace urls in breakdown
            poco.summarizedInsights.thumbnailUrl = newImageUrl;
            poco.breakdowns[0].thumbnailUrl = newImageUrl;

            await Task.WhenAll( poco.summarizedInsights.faces.Select(f => StoreFacesAsync(1, f, poco)));
           
        }

        private static async Task StoreFacesAsync(int v, Face f, VideoBreakdownPOCO poco)
        {
            var faceStream = await DownloadWebResource(f.thumbnailFullUrl);
            var blob = await UploadFileToBlobStorage(faceStream, $"{poco.id}/faces/{f.shortId}.jpg", "image/jpg");

            f.thumbnailFullUrl = blob.Uri.ToString();
        }

        private static async Task<CloudBlockBlob> UploadFileToBlobStorage(MemoryStream blobContents, string blobName, string contentType)
        {
            var resourcesContainer = Globals.VideoIndexerResourcesContainer;
            var newBlob = resourcesContainer.GetBlockBlobReference(blobName);
            newBlob.Properties.ContentType = contentType;
            await newBlob.UploadFromStreamAsync(blobContents);

            return newBlob;
        }

        private static async Task<MemoryStream> DownloadWebResource(string Url)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                return new MemoryStream(await httpClient.GetByteArrayAsync(Url));
            }
        }   

        private static async Task UpdateProcessingStateAsync(string VIUniqueId)
        {
            var collectionName = Globals.ProcessingStateCosmosCollectionName;
            var client = Globals.GetCosmosClient(collectionName);
            var collectionLink = UriFactory.CreateDocumentCollectionUri(Globals.CosmosDatabasename, collectionName);
            // first get the already existing document by id   
            ResourceResponse<Document> response;
            VIProcessingStatePOCO state;
            try
            {
                response = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(Globals.CosmosDatabasename, collectionName, VIUniqueId));
                state = (VIProcessingStatePOCO)(dynamic)response.Resource;
            }
            catch (Exception e)
            {
                // If document not found, probably an artifact of dev env where docs are being deleted occasionally midway 
                // through processing.  Just create a new one.
                state = new VIProcessingStatePOCO()
                {
                    id = VIUniqueId
                };
            }
             

            // update property values then upsert
            state.EndTime = DateTime.Now;
            response = await client.UpsertDocumentAsync(collectionLink, state);
            

        }

        private static async Task StoreBreakdownJsonInCosmos(VideoBreakdownPOCO videoBreakdownJson, TraceWriter log)
        {
            //string Cosmos_Collection_Name = ConfigurationManager.AppSettings["Cosmos_Collection_Name"];
            //if (String.IsNullOrEmpty(Cosmos_Collection_Name))
            //    throw new ApplicationException("Cosmos_Collection_Name app setting not set");

            var collectionName = "Breakdowns";
            var client = Globals.GetCosmosClient(collectionName);


            // save the json as a new document
            try
            {
                Document r = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(Globals.CosmosDatabasename, collectionName), videoBreakdownJson);
            }
            catch(Exception e)
            {
                Globals.LogMessage(log, $"error inserting document in cosmos: {e.Message}");
                // ignore for now, but maybe should replace the document if it already exists.. 
                // seems to be caused by dev environment where queue items are being reprocesssed
            }
        }

    }
}
