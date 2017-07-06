using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Blob;

namespace OrchestrationFunctions
{
    public static class AMSNotificationQueueHandler
    {


        static CloudMediaContext _context = null;

        /// <summary>
        /// This function will submit a Video Indexing job in response to an Azure Media Services 
        /// queue notification upon encoding completion.  To use this function, the AMS encoding task
        /// must be configured with a queue notification using the queue named below in the arguments
        /// to this method.  After the VI job completes, VI will use a web callback to invoke the rest
        /// of the processing in order to store the VI results in Cosmos Db. 
        /// </summary>
        /// <param name="myQueueItem"></param>
        /// <param name="log"></param>
        [FunctionName("AMSNotificationQueueHandler")]
        public static async Task RunAsync([QueueTrigger("encoding-complete", Connection = "AzureWebJobsStorage")]string myQueueItem, TraceWriter log)
        {
           
            NotificationMessage msg = JsonConvert.DeserializeObject<NotificationMessage>(myQueueItem);
            if (msg.EventType != NotificationEventType.JobStateChange)
                return; // ignore anything but job complete 

            //TODO: get SAS reference for one of the output renditions
            string newJobStateStr = (string)msg.Properties.Where(j => j.Key == "NewState").FirstOrDefault().Value;
            if (newJobStateStr == "Finished")
            {
                string jobId = msg.Properties["JobId"];
                string taskId = msg.Properties["TaskId"];

                _context = MediaServicesHelper.Context;

                var job = _context.Jobs.Where(j => j.Id == jobId).FirstOrDefault();
                var task = job.Tasks.Where(l => l.Id == taskId).FirstOrDefault();

                var outputAsset = task.OutputAssets[0];
                var inputAsset = task.InputAssets[0];
                var inputAssetId = inputAsset.Id;


                IAccessPolicy readPolicy = _context.AccessPolicies.Create("readPolicy",TimeSpan.FromHours(4), AccessPermissions.Read);
                ILocator outputLocator = _context.Locators.CreateLocator(LocatorType.Sas, outputAsset, readPolicy);

                //TODO: need a sas locator for the top bitrate rendition in the outputfiles

                // Submit processing job to Video Indexer
                string fileName = ""; // get this from AMS
                string SAS = "";

                await Globals.SubmitToVideoIndexerAsync(fileName, SAS);
            }


               
        }

        private static string PublishAsset(IAsset outputAsset)
        {
            
            // You cannot create a streaming locator using an AccessPolicy that includes write or delete permissions.
            IAccessPolicy policy = _context.AccessPolicies.Create("Streaming policy",
            TimeSpan.FromDays(30),
            AccessPermissions.Read);

            // Create a locator to the streaming content on an origin. 
            ILocator originLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, outputAsset,
            policy,
            DateTime.UtcNow.AddMinutes(-5));

            // Get a reference to the streaming manifest file from the  
            // collection of files in the asset. 
            var manifestFile = outputAsset.AssetFiles.Where(f => f.Name.ToLower().
                                   EndsWith(".ism")).
                                   FirstOrDefault();

            // Create a full URL to the manifest file. Use this for playback
            // in streaming media clients. 
            string urlForClientStreaming = originLocator.Path + manifestFile.Name + "/manifest";


            return urlForClientStreaming;
        }
        /*
* #r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
#r "System.Web"

#load "../helpers/Models.csx"
//#load "../helpers/copyBlobHelpers.csx"
//#load "../helpers/mediaServicesHelpers.csx"

using Microsoft.Azure;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


// Read values from the App.config file.
private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("MediaServicesAccountName");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("MediaServicesAccountKey");

private static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
private static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

// Field for service context.
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;

public static void Run(string myQueueItem, TraceWriter log, CloudTable tableBinding)
{
log.Info($"C# Queue trigger function processed: {myQueueItem}");

//=============================================
// Deserialize and parse AMS queue notification

EncodingQueueJobMessage msg = JsonConvert.DeserializeObject<EncodingQueueJobMessage>(myQueueItem);

// can ignore registration events
if (msg.EventType != "TaskStateChange")
return;

// get job and task id
string jobid;
string taskid;

if (!ValidateQueueMessage(msg, out jobid, out taskid, log))
throw new ApplicationException("Error with queue message. Not the expected AMS queue notification type");

// get the output asset of the job
// AMS account name and key from portal
_cachedCredentials = new MediaServicesCredentials(
           _mediaServicesAccountName, _mediaServicesAccountKey);

// Main object for interacting with AMS
_context = new CloudMediaContext(_cachedCredentials);

//IJob job = await Task.Run(() => _context.Jobs.Where(j => j.Id == jobid).FirstOrDefault());
IJob job = _context.Jobs.Where(j => j.Id == jobid).FirstOrDefault();
var t = job.Tasks.Where(l => l.Id == taskid).FirstOrDefault();

var outputAsset = t.OutputAssets[0];
var inputAsset = t.InputAssets[0];
var inputAssetId = inputAsset.Id;

var streamingUrl = BuildStreamingURLs(outputAsset);

// need to update row in table storage with streaming locator

// Create a retrieve operation that takes a customer entity.
TableOperation retrieveOperation = TableOperation.Retrieve<VideoEntity>("twins", inputAssetId);

// Execute the operation.
TableResult retrievedResult = tableBinding.Execute(retrieveOperation);

// Assign the result to a CustomerEntity object.
VideoEntity updateEntity = (VideoEntity)retrievedResult.Result;

if (updateEntity != null)
{
// Change the phone number.
updateEntity.StreamingUrl = streamingUrl;

// Create the Replace TableOperation.
TableOperation updateOperation = TableOperation.Replace(updateEntity);

// Execute the operation.
tableBinding.Execute(updateOperation);

Console.WriteLine("Entity updated.");
}
else
{
Console.WriteLine("Entity could not be retrieved.");
}



}

private static string BuildStreamingURLs(IAsset asset)
{

// Create a 30-day readonly access policy. 
// You cannot create a streaming locator using an AccessPolicy that includes write or delete permissions.
IAccessPolicy policy = _context.AccessPolicies.Create("Streaming policy",
TimeSpan.FromDays(30),
AccessPermissions.Read);

// Create a locator to the streaming content on an origin. 
ILocator originLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset,
policy,
DateTime.UtcNow.AddMinutes(-5));

// Display some useful values based on the locator.
Console.WriteLine("Streaming asset base path on origin: ");
Console.WriteLine(originLocator.Path);
Console.WriteLine();

// Get a reference to the streaming manifest file from the  
// collection of files in the asset. 
var manifestFile = asset.AssetFiles.Where(f => f.Name.ToLower().
                       EndsWith(".ism")).
                       FirstOrDefault();

// Create a full URL to the manifest file. Use this for playback
// in streaming media clients. 
string urlForClientStreaming = originLocator.Path + manifestFile.Name + "/manifest";


return urlForClientStreaming;

//Console.WriteLine("URL to manifest for client streaming using Smooth Streaming protocol: ");
//Console.WriteLine(urlForClientStreaming);
//Console.WriteLine("URL to manifest for client streaming using HLS protocol: ");
//Console.WriteLine(urlForClientStreaming + "(format=m3u8-aapl)");
//Console.WriteLine("URL to manifest for client streaming using MPEG DASH protocol: ");
//Console.WriteLine(urlForClientStreaming + "(format=mpd-time-csf)");
//Console.WriteLine();
}

public static bool ValidateQueueMessage(EncodingQueueJobMessage msg, out string JobId, out string TaskId, TraceWriter log)
{
//TODO: do more validation

JobId = string.Empty;
TaskId = string.Empty;
if (msg.Properties.Keys.Contains("JobId"))
JobId = msg.Properties["JobId"].ToString();
else
{
log.Info("No job id found in properties!");
return false;
}

if (msg.Properties.Keys.Contains("TaskId"))
TaskId = msg.Properties["TaskId"].ToString();
else
{
log.Info("No task id found in properties!");
return false;
}
return true;
}


* */
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
