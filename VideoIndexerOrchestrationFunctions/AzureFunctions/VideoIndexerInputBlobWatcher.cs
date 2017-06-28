using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using System;

namespace OrchestrationFunctions
{
    public static class VideoIndexerInputBlobWatcher
    {

        private static TraceWriter _log;
     

        [FunctionName("VideoIndexerInputBlobWatcher")]
        public static async System.Threading.Tasks.Task RunAsync([BlobTrigger("%Video_Input_Container%", Connection = "AzureWebJobsStorage")] CloudBlockBlob myBlob,
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
            string fileName = myBlob.Name;
            Globals.LogMessage(log, $"Blob named {fileName} being procesed by BlobWatcher function..");

            // get a SAS url for the blob       
            string SaSUrl = GetSasUrl(myBlob);
            Globals.LogMessage(log,$"Got SAS url {SaSUrl}");

            // call the api to process the video in VideoIndexer
            var VideoIndexerUniqueId = Globals.SubmitToVideoIndexerAsync(fileName, SaSUrl).Result;
            Globals.LogMessage(log, $"VideoId {VideoIndexerUniqueId} submitted to Video Indexer!");
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
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessStartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(1),
                Permissions = SharedAccessBlobPermissions.Read
            };

            //Generate the shared access signature on the blob, setting the constraints directly on the signature.
            string sasBlobToken = myBlob.GetSharedAccessSignature(sasConstraints);
            return myBlob.Uri + sasBlobToken;
        }
    }
}