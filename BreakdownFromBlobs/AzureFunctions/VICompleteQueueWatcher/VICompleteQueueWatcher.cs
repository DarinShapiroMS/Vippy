using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Configuration;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using System.Collections.Generic;
using Newtonsoft.Json;

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
            await SaveInCosmosAsync(poco);

        }

        private static async System.Threading.Tasks.Task SaveInCosmosAsync(VideoBreakdownPOCO videoBreakdownJson)
        {
            string endpoint = ConfigurationManager.AppSettings["cosmos_enpoint"];
            if (String.IsNullOrEmpty(endpoint))
                throw new ApplicationException("cosmos_enpoint app setting not set");

            string key = ConfigurationManager.AppSettings["cosmos_key"];
            if (String.IsNullOrEmpty(key))
                throw new ApplicationException("cosmos_key app setting not set");

            string cosmos_database_name = ConfigurationManager.AppSettings["cosmos_database_name"];
            if (String.IsNullOrEmpty(cosmos_database_name))
                throw new ApplicationException("cosmos_database_name app setting not set");

            string cosmos_collection_name = ConfigurationManager.AppSettings["cosmos_collection_name"];
            if (String.IsNullOrEmpty(cosmos_collection_name))
                throw new ApplicationException("cosmos_collection_name app setting not set");

            var client = new DocumentClient(new Uri(endpoint), key);

            // make sure the database and collection already exist
            await client.CreateDatabaseIfNotExistsAsync(new Database { Id = cosmos_database_name });
            await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(cosmos_database_name), new DocumentCollection { Id = cosmos_collection_name });

            // save the json as a new document
            Document r = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(cosmos_database_name, cosmos_collection_name), videoBreakdownJson);
        }
    }
}
