using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace OrchestrationFunctions
{
    public static class AMSInputBlobWatcher
    {
       
        [FunctionName("AMSInputBlobWatcher")]
        public static async Task RunAsync([BlobTrigger("encoding-input/{name}.mp4", Connection = 
            "AzureWebJobsStorage")] CloudBlockBlob inputVideoBlob,      // video blob that initiated this function
            [Blob("encoding-input/{name}.json", FileAccess.Read)] string manifestContents,  // if a json file with the same name exists, it's content will be in this variable.
            [Queue("ams-input")] IAsyncCollector<string> outputQueue,   // output queue for async processing and resiliency
            TraceWriter log)
        {
            //================================================================================
            // Function AMSInputBlobWatcher
            // Purpose:
            // This function monitors a blob container for new mp4 video files (TODO:// update
            // filter to include all video formats supported by MES).  If the video files are 
            // accompanied by a json file with the same file name, it will use this json file
            // for metadata such as video title, external ids, etc.  Any custom fields added
            // to this meta data file will be stored with the resulting document in Cosmos. 
            // ** Rather than doing any real processing here, just forward the payload to a
            // queue to be more resilient. A client app can either post files to the storage
            // container or add items to the queue directly.  Aspera or Signiant users will 
            // most likely opt to use the watch folder. 
            // ** NOTE - the json file must be dropped into the container first. 
            //================================================================================
            
            
            // if metadata json was used, get it's values as a dictionary
            var metaDataDictionary = 
                !string.IsNullOrEmpty(manifestContents) 
                ? JsonConvert.DeserializeObject<Dictionary<string, string>>(manifestContents) 
                : new Dictionary<string, string>();

            // work out the global id for this video. If internal_id was in manifest json, use that. 
            // Otherwise create a new one
            var globalId = metaDataDictionary.ContainsKey("internal_id") 
                ? metaDataDictionary["internal_id"] 
                : Guid.NewGuid().ToString();

            // add values to the state variable that is stored in Cosmos to keep track
            // of various stages of processing, which also allows passing values from the json manifest
            // file to the final document stored in Cosmos
            var state = new VippyProcessingState
            {
                Id = globalId,               
                BlobName = inputVideoBlob.Name,
                StartTime = DateTime.Now,
                CustomProperties = metaDataDictionary,
            };


            Globals.LogMessage(log, $"Video '{inputVideoBlob.Name}' landed in watch folder" + (!string.IsNullOrEmpty(manifestContents) 
                ? " with manifest json": "without manifest file"));

            await outputQueue.AddAsync(JsonConvert.SerializeObject(state));

      }
    }
}