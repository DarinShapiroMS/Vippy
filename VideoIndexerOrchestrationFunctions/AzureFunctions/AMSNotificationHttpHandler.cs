using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace OrchestrationFunctions
{
    public static class AMSNotificationHttpHandler
    {
        [FunctionName("AMSNotificationHttpHandler")]
        public static async Task<object> Run(
            [HttpTrigger(WebHookType = "genericJson", Route = "amscallback")] HttpRequestMessage req,
            [Queue("encoding-complete", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> outputQueue,
            TraceWriter log)
        {
            /*
 Finished JSON message
{
  "MessageVersion": "1.1",
  "ETag": "7a1526f15c0e40f6ea916eef6e9fbba0a03d6f7b7ecf69b136b4bdde01895cde",
  "EventType": "TaskStateChange",
  "TimeStamp": "2017-07-07T00:35:29.0827372Z",
  "Properties": {
    "JobId": "nb:jid:UUID:5be1b2ff-0300-80c0-6c7b-f1e762ab3c2e",
    "TaskId": "nb:tid:UUID:5be1b2ff-0300-80c0-6c7c-f1e762ab3c2e",
    "NewState": "Finished",
    "OldState": "Processing",
    "AccountName": "viapipoc9hckf5jj2twnea",
    "AccountId": "4f15b61c-c731-4295-a5d7-7993bd938ab5",
    "NotificationEndPointId": "nb:nepid:UUID:644747ca-a869-4791-a5a0-e8a98ff6dbf8"
  }
}

Registration JSON message
{
  "MessageVersion": "1.0",
  "ETag": "66d7a15b3291eb84d6864aff4a39d421d9b29332bd01cde845b3391a1994e399",
  "EventType": 2,
  "TimeStamp": "2017-07-07T00:57:35.5576705Z",
  "Properties": {
    "NotificationEndPointId": "nb:nepid:UUID:6dc80709-70f6-490a-9634-5c8acb810440",
    "State": "Registered",
    "Name": "FunctionWebHook",
    "Created": "2017-07-07T00:57:35",
    "AccountId": "4f15b61c-c731-4295-a5d7-7993bd938ab5"
  }

             * 
             */

            log.Info($"Webhook was triggered by {req.Headers.UserAgent}");


            var jsonContent = await req.Content.ReadAsStringAsync();
            await outputQueue.AddAsync(jsonContent);
            //dynamic data = JsonConvert.DeserializeObject(jsonContent);

            //log.Info($"Input JSON is-{jsonContent}");

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}