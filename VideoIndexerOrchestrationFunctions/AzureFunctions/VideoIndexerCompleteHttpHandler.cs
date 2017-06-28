using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;
using System.Configuration;

namespace OrchestrationFunctions
{
    public static class VideoIndexerCompleteHttpHandler
    {
        /// <summary>
        /// All this function does is take the input from the http trigger invocation and place it in a queue.  This
        /// way any downstream processing is more reliable and resiliant to errors, transient or otherwise
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <param name="outputQueue"></param>
        /// <returns></returns>
        [FunctionName("Webhook2Queue")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "vicallback")]HttpRequestMessage req,  
            TraceWriter log, 
            [Queue("vi-processing-complete", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> outputQueue
            )
        {
            Globals.LogMessage(log,"Webhook2Queue function called");

            var queryParams =  req.GetQueryNameValuePairs().ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);


            string queueJson = "";
            if (queryParams != null)
            {
                queueJson =  JsonConvert.SerializeObject(queryParams, Formatting.Indented);

                await outputQueue.AddAsync(queueJson);

                //foreach (string keyName in queryParams.Keys)
                //{
                //    Globals.LogMessage(log,$"Form variable named {keyName} found posted to httpTrigger");
                //} 
            }
            
            return req.CreateResponse(HttpStatusCode.OK, queueJson);
        }
    }
}