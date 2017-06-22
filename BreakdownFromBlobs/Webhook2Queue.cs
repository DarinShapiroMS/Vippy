using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;

namespace OrchestrationFunction
{
    public static class Webhook2Queue
    {
        [FunctionName("Webhook2Queue")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "vicallback")]HttpRequestMessage req,  TraceWriter log, [Queue("vi-processing-complete", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> outputQueue)
        {
            log.Info("Webhook2Queue function called");

            var queryParams =  req.GetQueryNameValuePairs().ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);


            string queueJson = "";
            if (queryParams != null)
            {
                queueJson =  JsonConvert.SerializeObject(queryParams, Formatting.Indented);

                await outputQueue.AddAsync(queueJson);

                foreach (string keyName in queryParams.Keys)
                {
                    log.Info($"Form variable named {keyName} found posted to httpTrigger");
                } 
            }

            
           
            // Fetching the name from the path parameter in the request URL
            return req.CreateResponse(HttpStatusCode.OK, queueJson);
        }
    }
}