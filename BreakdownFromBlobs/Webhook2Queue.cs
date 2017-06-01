using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;
using System;

namespace OrchestrationFunction
{
    public static class Webhook2Queue
    {
        [FunctionName("Webhook2Queue")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "HttpTriggerCSharp/name/{name}")]HttpRequestMessage req, string name, TraceWriter log, [Queue("vi-processing-complete", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> outputQueue)
        {
            log.Info("Webhook2Queue function called");

            var queryParams =  req.GetQueryNameValuePairs().ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            if (queryParams != null)
            {
                foreach (string keyName in queryParams.Keys)
                {
                    log.Info($"Form variable named {keyName} found posted to httpTrigger");
                } 
            }

            await outputQueue.AddAsync(name);
           
            // Fetching the name from the path parameter in the request URL
            return req.CreateResponse(HttpStatusCode.OK, "Hello " + name);
        }
    }
}