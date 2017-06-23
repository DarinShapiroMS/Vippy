using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Configuration;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace OrchestrationFunctions
{
    public static class VideoIndexerCompleteQueueWatcher
    {



        [FunctionName("VideoIndexingComplete")]
        public static async System.Threading.Tasks.Task RunAsync([QueueTrigger("vi-processing-complete", Connection = "AzureWebJobsStorage")]string myQueueItem, TraceWriter log)
        {

            log.Info($"C# Queue trigger function processed: {myQueueItem}");

            // queue item should be id & state
            Dictionary<string, string> completionData = JsonConvert.DeserializeObject<Dictionary<string, string>>(myQueueItem);

            var apiUrl = Globals.VideoIndexerApiUrl;
            var client = Globals.GetVideoIndexerHttpClient();
            var result = client.GetAsync(string.Format(apiUrl + "/{0}", completionData["id"])).Result;
            var json = result.Content.ReadAsStringAsync().Result;

            var poco = JsonConvert.DeserializeObject<VideoBreakdownPOCO>(json);
            await StoreBreakdownJsonInCosmos(poco);
            await UpdateProcessingStateAsync(completionData["id"]);

        }

        private static async Task UpdateProcessingStateAsync(string VIUniqueId)
        {
            var collectionName = "VIProcessingState";
            var client = Globals.GetCosmosClient(collectionName);

            // first get the already existing document by id         
            var response = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(Globals.CosmosDatabasename, collectionName, VIUniqueId));
            VIProcessingStatePOCO state = (VIProcessingStatePOCO)(dynamic)response.Resource;

            // update property values then replace
            state.EndTime = DateTime.Now;
            response = await client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(Globals.CosmosDatabasename, collectionName, state.VIUniqueId), state);

        }

        private static async System.Threading.Tasks.Task StoreBreakdownJsonInCosmos(VideoBreakdownPOCO videoBreakdownJson)
        {
            //string cosmos_collection_name = ConfigurationManager.AppSettings["cosmos_collection_name"];
            //if (String.IsNullOrEmpty(cosmos_collection_name))
            //    throw new ApplicationException("cosmos_collection_name app setting not set");

            var collectionName = "Breakdowns";
            var client = Globals.GetCosmosClient(collectionName);

           
            // save the json as a new document
            Document r = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(Globals.CosmosDatabasename, collectionName), videoBreakdownJson);
        }
    }
}
