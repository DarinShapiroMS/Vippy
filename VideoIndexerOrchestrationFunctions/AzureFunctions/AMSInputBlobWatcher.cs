using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Routing;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OrchestrationFunctions
{
    public static class AMSInputBlobWatcher
    {
       
        [FunctionName("AMSInputBlobWatcher")]
        public static async Task RunAsync([BlobTrigger("%amsBlobInputContainer%/{name}.{extension}", Connection = 
            "AzureWebJobsStorage")] CloudBlockBlob inputVideoBlob,      // video blob that initiated this function
            [Blob("%amsBlobInputContainer%/{name}.json", FileAccess.Read)] string manifestContents,  // if a json file with the same name exists, it's content will be in this variable.
            [Queue("ams-input")] IAsyncCollector<string> outputQueue,   // output queue for async processing and resiliency
            TraceWriter log)
        {
            //================================================================================
            // Function AMSInputBlobWatcher
            // Purpose:
            // This function monitors a blob container for new mp4 video files.  If the video files are 
            // accompanied by a json file with the same file name, it will use this json file
            // for metadata such as video title, external ids, etc.  Any custom fields added
            // to this meta data file will be stored with the resulting document in Cosmos. 
            // ** Rather than doing any real processing here, just forward the payload to a
            // queue to be more resilient. A client app can either post files to the storage
            // container or add items to the queue directly.  Aspera or Signiant users will 
            // most likely opt to use the watch folder. 
            // ** NOTE - the json file must be dropped into the container first. 
            //================================================================================
            
            //HACK: This isn't ideal. I'd rather the trigger for this function NOT kick off
            // for json files.  That way all the app insights metrics aren't polluted with 
            // eroneous runs.
            if(inputVideoBlob.Name.ToLower().EndsWith(".json"))
                return;

            VippyProcessingState manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<VippyProcessingState>(manifestContents);
            }
            catch (Exception e)
            {
                //TODO: wrap up nicely for AppInsights
                throw new ApplicationException($"Invalid manifest file provided for video {inputVideoBlob.Name}");
            }
                                   
            // work out the global id for this video. If internal_id was in manifest json, use that. 
            // Otherwise create a new one
            var internalId = manifest.AlternateId;
            var globalId = !string.IsNullOrEmpty(internalId) ? 
                internalId : 
                Guid.NewGuid().ToString();

            // stuff it back into the manifest
            manifest.AlternateId = globalId;
            manifest.BlobName = inputVideoBlob.Name;
            manifest.StartTime = DateTime.Now;


            Globals.LogMessage(log, $"Video '{inputVideoBlob.Name}' landed in watch folder" + (manifestContents != null ? 
                 " with manifest json": "without manifest file"));

            await outputQueue.AddAsync(manifest.ToString());

        }
    }
}