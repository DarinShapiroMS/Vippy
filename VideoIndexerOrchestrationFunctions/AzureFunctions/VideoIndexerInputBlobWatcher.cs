using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;

namespace OrchestrationFunctions
{
    public static class VideoIndexerInputBlobWatcher
    {
        private static TraceWriter _log;


        [FunctionName("VideoIndexerInputBlobWatcher")]
        public static async Task RunAsync(
            [BlobTrigger("%Video_Input_Container%", Connection = "AzureWebJobsStorage")] CloudBlockBlob myBlob,
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

            // blob filename
            var fileName = myBlob.Name;
            Globals.LogMessage(log, $"Blob named {fileName} being procesed by BlobWatcher function..");

            // get a SAS url for the blob       
            var SaSUrl = Globals.GetSasUrl(myBlob);
            Globals.LogMessage(log, $"Got SAS url {SaSUrl}");

            // call the api to process the video in VideoIndexer
            var VideoIndexerUniqueId = Globals.SubmitToVideoIndexerAsync(fileName, SaSUrl).Result;
            Globals.LogMessage(log, $"VideoId {VideoIndexerUniqueId} submitted to Video Indexer!");
        }
    }
}