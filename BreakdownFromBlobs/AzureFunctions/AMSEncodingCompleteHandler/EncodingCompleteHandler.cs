using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace OrchestrationFunctions.AzureFunctions.AMSEncodingCompleteHandler
{
    public static class EncodingCompleteHandler
    {
        [FunctionName("AMSEncodingCompleteHandler")]        
        public static void Run([QueueTrigger("myqueue-items", Connection = "AzureWebJobsStorage")]string myQueueItem, TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {myQueueItem}");
        }

        private static void Initialize()
        {
            Console.WriteLine("SDF");
        }
    }
}
