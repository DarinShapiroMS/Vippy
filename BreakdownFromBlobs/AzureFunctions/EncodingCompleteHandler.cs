using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;

namespace OrchestrationFunctions
{
    public static class EncodingCompleteHandler
    {
        /// <summary>
        /// This function will submit a Video Indexing job in response to an Azure Media Services 
        /// queue notification upon encoding completion.  To use this function, the AMS encoding task
        /// must be configured with a queue notification using the queue named below in the arguments
        /// to this method.  After the VI job completes, VI will use a web callback to invoke the rest
        /// of the processing in order to store the VI results in Cosmos Db. 
        /// </summary>
        /// <param name="myQueueItem"></param>
        /// <param name="log"></param>
        [FunctionName("AMSEncodingCompleteHandler")]
        public static async Task RunAsync([QueueTrigger("encoding-complete", Connection = "AzureWebJobsStorage")]string myQueueItem, TraceWriter log)
        {
            //TODO: validate AMS job notification.
            //TODO: get SAS reference for one of the output renditions

            // Submit processing job to Video Indexer
            string fileName = ""; // get this from AMS
            string SAS = "";

            await Globals.SubmitToVideoIndexerAsync(fileName, SAS);
        }

    }
}
