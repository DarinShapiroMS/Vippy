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
    public static class VideoIndexerInputBlobWatcher
    {
        private static TraceWriter _log;


        [FunctionName("VideoIndexerInputBlobWatcher")]
        public static async Task RunAsync(
            [BlobTrigger("%videoIndxerBlobInputContainer%/{name}.mp4", Connection = "AzureWebJobsStorage")] CloudBlockBlob myBlob,
            [Blob("%videoIndxerBlobInputContainer%/{name}.json", FileAccess.Read)] string manifestContents,  // if a json file with the same name exists, it's content will be in this variable.
            string name,
            TraceWriter log
        )
        {
            // =============================================================================================
            // This function is only used to watch a blob container when you want video files to be submitted
            // directly to Video Indexer, outside of Azure Media Services.  Just upload a video file to 
            // the input directory and this function will submit the video to Video Indexer, and the results
            // will be stored in Cosmos Db when processing is complete
            // =============================================================================================

            _log = log;

            // TODO: validate file types here or add file extension filters to blob trigger
            // TODO: move all this into a queue based function. Too much here for blob watcher

            // blob filename
            var fileName = myBlob.Name;
            Globals.LogMessage(log, $"Blob named {fileName} being procesed by BlobWatcher function..");


            // if metadata json was used, get it's values as a dictionary
            var metaDataDictionary =
                !string.IsNullOrEmpty(manifestContents)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(manifestContents)
                    : new Dictionary<string, string>();

            // add a variable to state to indicate this was initiated via VideoIndexer watch folder, 
            // not via the beginning of the pipeline and ams encoding.
            metaDataDictionary.Add("processingStartedFrom", "VideoIndexerWatchFolder");

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
                BlobName = fileName,
                StartTime = DateTime.Now,
                CustomProperties = metaDataDictionary,
            };

            // update processing progress with id and metadata payload
            await Globals.StoreProcessingStateRecordInCosmosAsync(state);

            // get a SAS url for the blob       
            var sasUrl = Globals.GetSasUrl(myBlob);
            Globals.LogMessage(log, $"Got SAS url {sasUrl}");

            // call the api to process the video in VideoIndexer
            var videoIndexerUniqueId = Globals.SubmitToVideoIndexerAsync(fileName, sasUrl, globalId).Result;
            Globals.LogMessage(log, $"VideoId {videoIndexerUniqueId} submitted to Video Indexer!");
        }
    }
}