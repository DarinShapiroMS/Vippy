using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace OrchestrationFunctions
{
    public class Globals
    {
        public static string VideoIndexerApiUrl = "https://videobreakdown.azure-api.net/Breakdowns/Api/Partner/Breakdowns";


        public static HttpClient GetVideoIndexerHttpClient()
        {
            var client = new HttpClient();

            // Video Indexer API key stored in settings (App Settings in Azure Function portal)
            string VideoIndexerKey = ConfigurationManager.AppSettings["video_indexer_key"];
            if (String.IsNullOrEmpty(VideoIndexerKey))
                throw new ApplicationException("VideoIndexerKey app setting not set");

           
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", VideoIndexerKey);
            return client;
        }
    }
}
