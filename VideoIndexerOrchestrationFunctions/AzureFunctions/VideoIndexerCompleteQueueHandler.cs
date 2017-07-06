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

        private static async Task StoreBreakdownJsonInCosmos(VideoBreakdownPOCO videoBreakdownJson)
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
                // ignore for now, but maybe should replace the document if it already exists.. 
                // seems to be caused by dev environment where queue items are being reprocesssed
            }
        }

    }
}
