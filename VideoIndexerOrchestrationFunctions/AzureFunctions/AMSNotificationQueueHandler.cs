#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

#endregion

namespace OrchestrationFunctions
{
    public static class AmsNotificationQueueHandler
    {
        private static CloudMediaContext _context;

        /// <summary>
        ///     This function will submit a Video Indexing job in response to an Azure Media Services
        ///     queue notification upon encoding completion.  To use this function, the AMS encoding task
        ///     must be configured with a queue notification using the queue named below in the arguments
        ///     to this method.  After the VI job completes, VI will use a web callback to invoke the rest
        ///     of the processing in order to store the VI results in Cosmos Db.
        /// </summary>
        /// <param name="myQueueItem"></param>
        /// <param name="log"></param>
        [FunctionName("AMSNotificationQueueHandler")]
        public static async Task RunAsync(
            [QueueTrigger("encoding-complete", Connection = "AzureWebJobsStorage")] string myQueueItem, TraceWriter log)
        {
            var msg = JsonConvert.DeserializeObject<NotificationMessage>(myQueueItem);
            if (msg.EventType != NotificationEventType.TaskStateChange)
                return; // ignore anything but job complete 


            var newJobStateStr = msg.Properties.FirstOrDefault(j => j.Key == "NewState").Value;
            if (newJobStateStr == "Finished")
            {
               
                var jobId = msg.Properties["JobId"];
                var taskId = msg.Properties["TaskId"];

                _context = MediaServicesHelper.Context;

                var job = _context.Jobs.Where(j => j.Id == jobId).FirstOrDefault();
                var task = job.Tasks.Where(l => l.Id == taskId).FirstOrDefault();

                var outputAsset = task.OutputAssets[0];
                var inputAsset = task.InputAssets[0];
                var alternateId = inputAsset.AlternateId;

                // get state record for tracking some ams related values
                var state = await Globals.GetProcessingStateRecord(alternateId);
                
                // for illustration, lets store the AMS encoding jobs running duration in state
                state.CustomProperties.Add("amsProcessingDuration", job.RunningDuration.Seconds.ToString());

                var readPolicy =
                    _context.AccessPolicies.Create("readPolicy", TimeSpan.FromHours(4), AccessPermissions.Read);
                var outputLocator = _context.Locators.CreateLocator(LocatorType.Sas, outputAsset, readPolicy);

                var destBlobStorage = CopyBlobHelper.AmsStorageAccount.CreateCloudBlobClient();

                // Get the asset container reference
                var outContainerName = new Uri(outputLocator.Path).Segments[1];
                var outContainer = destBlobStorage.GetContainerReference(outContainerName);

                // use largest single mp4 output (highest bitrate) to send to Video Indexer
                var biggestblob = outContainer.ListBlobs().OfType<CloudBlockBlob>()
                    .Where(b => b.Name.ToLower().EndsWith(".mp4"))
                    .OrderBy(u => u.Properties.Length).Last();

                var sas = Globals.GetSasUrl(biggestblob);

                // Submit processing job to Video Indexer
                await Globals.SubmitToVideoIndexerAsync(biggestblob.Name, sas, inputAsset.AlternateId);
            }
        }

        private static string PublishAsset(IAsset outputAsset)
        {
            // You cannot create a streaming locator using an AccessPolicy that includes write or delete permissions.
            var policy = _context.AccessPolicies.Create("Streaming policy",
                TimeSpan.FromDays(30),
                AccessPermissions.Read);

            // Create a locator to the streaming content on an origin. 
            var originLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, outputAsset,
                policy,
                DateTime.UtcNow.AddMinutes(-5));

            // Get a reference to the streaming manifest file from the  
            // collection of files in the asset. 
            var manifestFile = outputAsset.AssetFiles.Where(f => f.Name.ToLower().EndsWith(".ism")).FirstOrDefault();

            // Create a full URL to the manifest file. Use this for playback
            // in streaming media clients. 
            var urlForClientStreaming = originLocator.Path + manifestFile.Name + "/manifest";


            return urlForClientStreaming;
        }
    }

    internal enum NotificationEventType
    {
        None = 0,
        JobStateChange = 1,
        NotificationEndPointRegistration = 2,
        NotificationEndPointUnregistration = 3,
        TaskStateChange = 4,
        TaskProgress = 5
    }

    internal sealed class NotificationMessage
    {
        public string MessageVersion { get; set; }
        public string ETag { get; set; }
        public NotificationEventType EventType { get; set; }
        public DateTime TimeStamp { get; set; }
        public IDictionary<string, string> Properties { get; set; }
    }
}