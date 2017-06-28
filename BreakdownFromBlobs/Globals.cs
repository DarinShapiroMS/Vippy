using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="SaSUrl">Secure link to video file in Azure Storage</param>
        /// <returns>VideoBreakdown in JSON format</returns>
        public static async Task<string> SubmitToVideoIndexerAsync(string blobName, string SaSUrl)
        {

            string Video_Indexer_Callback_url = ConfigurationManager.AppSettings["Video_Indexer_Callback_url"];
            if (String.IsNullOrEmpty(Video_Indexer_Callback_url))
                throw new ApplicationException("Video_Indexer_Callback_url app setting not set");


            var queryString = HttpUtility.ParseQueryString(string.Empty);
            // These can be used to set meta data visible in the VI portal.  
            // required settings
            queryString["videoUrl"] = SaSUrl;
            queryString["language"] = "English";
            queryString["privacy"] = "private";

            // optional settings - mostly VI portal UI related
            queryString["name"] = "Fashion Video";
            queryString["description"] = "A video of a fashion model";
            queryString["callbackUrl"] = Video_Indexer_Callback_url;
            //queryString["externalId"] = "{string}";   // Use this to track AMS Asset ID, or even unqique customer ID for the video
            //queryString["metadata"] = "{string}";
            //queryString["partition"] = "{string}";


            var apiUrl = Globals.VideoIndexerApiUrl;
            var client = Globals.GetVideoIndexerHttpClient();

            // post to the API
            var result = await client.PostAsync(apiUrl + $"?{queryString}", null);

            // the JSON result in this case is the VideoIndexer assigned ID for this video.
            var json = result.Content.ReadAsStringAsync().Result;
            var VideoIndexerId = JsonConvert.DeserializeObject<string>(json);

            // save a record of this job submission
            await StoreProcessingStateRecordInCosmosAsync(blobName, VideoIndexerId);
            // delete the breakdown
            //DeleteBreakdown();
            // don't delete for now since we need to use the VI portal to train faces

            return VideoIndexerId;
        }
        /// <summary>
        /// Inserts a receipt like record in the database. This record will be updated when the processing
        /// is completed with success or error details
        /// </summary>
        /// <param name="videoIndexerId"></param>
        /// <returns></returns>
        private static async Task StoreProcessingStateRecordInCosmosAsync(string blobName, string videoIndexerId)
        {
            var collectionName = Globals.ProcessingStateCosmosCollectionName;
            var client = Globals.GetCosmosClient(collectionName);

            var state = new VIProcessingStatePOCO()
            {
                BlobName = blobName,
                id = videoIndexerId,
                StartTime = DateTime.Now,
                EndTime = null,
                ErrorMessage = ""
            };

            // save the json as a new document
            Document r = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(Globals.CosmosDatabasename, collectionName), state);

        }


        /// <summary>
        /// Returns a new DocumentClient instantiated with endpoint and key
        /// </summary>
        /// <returns></returns>
        public static DocumentClient GetCosmosClient()
        {
            string endpoint = ConfigurationManager.AppSettings["cosmos_enpoint"];
            if (String.IsNullOrEmpty(endpoint))
                throw new ApplicationException("cosmos_enpoint app setting not set");

            string key = ConfigurationManager.AppSettings["cosmos_key"];
            if (String.IsNullOrEmpty(key))
                throw new ApplicationException("cosmos_key app setting not set");

            var client = new DocumentClient(new Uri(endpoint), key);

            return client;
        }

        /// <summary>
        /// Returns a new DocumentClient instantiated with endpoint and key, AND
        /// creates the database and collection they don't already exist. 
        /// </summary>
        /// <param name="database"></param>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static DocumentClient GetCosmosClient( string collection)
        {
            var client = GetCosmosClient();

            // ensure database and collection exist
            CreateCosmosDbAndCollectionIfNotExists(client, CosmosDatabasename, collection);

            return client;
        }

        /// <summary>
        /// This makes sure the database and collection exist.  It will create them 
        /// in the event they don't. Makes deployment cleaner.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="database"></param>
        /// <param name="collection"></param>
        private static async void CreateCosmosDbAndCollectionIfNotExists(DocumentClient client, string database, string collection)
        {
            // make sure the database and collection already exist           
            await client.CreateDatabaseIfNotExistsAsync(new Database { Id = database });
            await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(database), new DocumentCollection { Id = collection });

        }

        public static string CosmosDatabasename {
            get
            {
                string cosmos_database_name = ConfigurationManager.AppSettings["cosmos_database_name"];
                if (String.IsNullOrEmpty(cosmos_database_name))
                    throw new ApplicationException("cosmos_database_name app setting not set");
                return cosmos_database_name;

            }

        }

        public static string ProcessingStateCosmosCollectionName {
            get
            {
                return "VIProcessingState";
            }
            set { }
        }

        /// <summary>
        /// Simple wrapper to write trace messages with a prefix (makes it more 
        /// readable in the output windows)
        /// </summary>
        /// <param name="log"></param>
        /// <param name="message"></param>
        public static void LogMessage(TraceWriter log, string message, [CallerFilePath]string callerFilePath = "")
        {
            callerFilePath = Path.GetFileName(callerFilePath);
            log.Info($"*** Function '{callerFilePath}' user trace ***  {message}");
        }
    }
}
