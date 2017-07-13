using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace OrchestrationFunctions
{
    public class Globals
    {
        public static readonly string VideoIndexerApiUrl =
            "https://videobreakdown.azure-api.net/Breakdowns/Api/Partner/Breakdowns";


        static Globals()
        {
            // initialize VI resources container
            var amsStorageClient = CopyBlobHelper.AmsStorageAccount.CreateCloudBlobClient();
            var imageContainer = amsStorageClient.GetContainerReference("video-indexer-resources");

            if (imageContainer.CreateIfNotExists())
            {
                // configure container for public access
                var permissions = imageContainer.GetPermissions();
                permissions.PublicAccess = BlobContainerPublicAccessType.Container;
                imageContainer.SetPermissions(permissions);
            }
            VideoIndexerResourcesContainer = imageContainer;
        }


        /// <summary>
        ///     Gets a URL with a SAS token that is good to read the file for 1 hour
        /// </summary>
        /// <param name="myBlob"></param>
        /// <returns></returns>
        public static string GetSasUrl(CloudBlockBlob myBlob)
        {
            // expiry time set 5 minutes in the past to 1 hour in the future. THis can be
            // moved into configuration if needed
            var sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessStartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(1),
                Permissions = SharedAccessBlobPermissions.Read
            };

            //Generate the shared access signature on the blob, setting the constraints directly on the signature.
            var sasBlobToken = myBlob.GetSharedAccessSignature(sasConstraints);
            return myBlob.Uri + sasBlobToken;
        }

        /// <summary>
        ///     Simple wrapper to write trace messages with a prefix (makes it more
        ///     readable in the output windows)
        /// </summary>
        /// <param name="log"></param>
        /// <param name="message"></param>
        public static void LogMessage(TraceWriter log, string message, [CallerFilePath] string callerFilePath = "")
        {
            callerFilePath = Path.GetFileName(callerFilePath);
            log.Info($"*** Function '{callerFilePath}' user trace ***  {message}");
        }

        #region Properties

        public static CloudBlobContainer VideoIndexerResourcesContainer { get; set; }

        public static string CosmosDatabasename
        {
            get
            {
                var cosmosDatabaseName = ConfigurationManager.AppSettings["Cosmos_Database_Name"];
                if (string.IsNullOrEmpty(cosmosDatabaseName))
                    throw new ApplicationException("Cosmos_Database_Name app setting not set");
                return cosmosDatabaseName;
            }
        }

        public static string ProcessingStateCosmosCollectionName => "VIProcessingState";

        #endregion

        #region Cosmos Methods

        /// <summary>
        ///     Returns a new DocumentClient instantiated with endpoint and key
        /// </summary>
        /// <returns></returns>
        private static DocumentClient GetCosmosClient()
        {
            var endpoint = ConfigurationManager.AppSettings["Cosmos_Endpoint"];
            if (string.IsNullOrEmpty(endpoint))
                throw new ApplicationException("Cosmos_Endpoint app setting not set");

            var key = ConfigurationManager.AppSettings["Cosmos_Key"];
            if (string.IsNullOrEmpty(key))
                throw new ApplicationException("Cosmos_Key app setting not set");

            var client = new DocumentClient(new Uri(endpoint), key);

            return client;
        }

        /// <summary>
        ///     Returns a new DocumentClient instantiated with endpoint and key, AND
        ///     creates the database and collection they don't already exist.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static DocumentClient GetCosmosClient(string collection)
        {
            var client = GetCosmosClient();

            // ensure database and collection exist
            CreateCosmosDbAndCollectionIfNotExists(client, CosmosDatabasename, collection);

            return client;
        }

        /// <summary>
        ///     This makes sure the database and collection exist.  It will create them
        ///     in the event they don't. Makes deployment cleaner.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="database"></param>
        /// <param name="collection"></param>
        private static async void CreateCosmosDbAndCollectionIfNotExists(DocumentClient client, string database,
            string collection)
        {
            // make sure the database and collection already exist           
            await client.CreateDatabaseIfNotExistsAsync(new Database {Id = database});
            await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(database),
                new DocumentCollection {Id = collection});
        }


        /// <summary>
        ///     Inserts a receipt like record in the database. This record will be updated when the processing
        ///     is completed with success or error details
        /// </summary>
        /// <param name="state">metadata provided with input video (manifest type data)</param>
        /// <returns></returns>
        public static async Task StoreProcessingStateRecordInCosmosAsync(VippyProcessingState state)
        {

            var collectionName = ProcessingStateCosmosCollectionName;
            var client = GetCosmosClient(collectionName);


            // upsert the json as a new document
            try
            {
                Document r =
                       await client.UpsertDocumentAsync(
                           UriFactory.CreateDocumentCollectionUri(CosmosDatabasename, collectionName), state);
            }
            catch (Exception e)
            {

                throw new ApplicationException($"Error in StoreProcessingStateRecordInCosmosAsync:/r/n{e.Message}");
            }
        }

        public static async Task<VippyProcessingState> GetProcessingStateRecord(string alternateId)
        {
            var collectionName = ProcessingStateCosmosCollectionName;
            var client = GetCosmosClient(collectionName);

            ResourceResponse<Document> response;

            try
            {
                response = await client.ReadDocumentAsync(
                       UriFactory.CreateDocumentUri(CosmosDatabasename, collectionName, alternateId));
            }
            catch (Exception e)
            {

                throw new ApplicationException($"Error in GetProcessingStateRecord() reading doc by id:\r\n{e.Message}");
            }

            Console.WriteLine("Document read by Id {0}", response.Resource);
            Console.WriteLine("RU Charge for reading a Document by Id {0}", response.RequestCharge);

            VippyProcessingState state;
            try
            {
                state = (VippyProcessingState)(dynamic)response.Resource;
            }
            catch (Exception e2)
            {

                throw new ApplicationException($"Error in GetProcessingStateRecord() casting response.resource to processing state:\r\n{e2.Message}");
            }

            return state;
        }

        #endregion

        #region Video Indexer Methods

        public static HttpClient GetVideoIndexerHttpClient()
        {
            var client = new HttpClient();

            // Video Indexer API key stored in settings (App Settings in Azure Function portal)
            var videoIndexerKey = ConfigurationManager.AppSettings["VideoIndexer_Key"];
            if (string.IsNullOrEmpty(videoIndexerKey))
                throw new ApplicationException("VideoIndexerKey app setting not set");


            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", videoIndexerKey);
            return client;
        }

        /// <summary>
        /// </summary>
        /// <param name="SaSUrl">Secure link to video file in Azure Storage</param>
        /// <returns>VideoBreakdown in JSON format</returns>
        public static async Task<string> SubmitToVideoIndexerAsync(string blobName, string SaSUrl,
            string alternateId)
        {
            // need to get the processing state to set some of the properties on the VI job
            var state = await GetProcessingStateRecord(alternateId);
            var props = state.CustomProperties;

            var videoIndexerCallbackUrl = ConfigurationManager.AppSettings["Video_Indexer_Callback_url"];
            if (string.IsNullOrEmpty(videoIndexerCallbackUrl))
                throw new ApplicationException("Video_Indexer_Callback_url app setting not set");


            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // These can be used to set meta data visible in the VI portal.  
            // required settings
            queryString["videoUrl"] = SaSUrl;
            queryString["language"] = props.ContainsKey("video_language") ? props["video_language"] : "English";
            queryString["privacy"] = "private";

            // optional settings - mostly VI portal UI related
            queryString["name"] = props.ContainsKey("video_title") ? props["video_title"] : state.BlobName;
            queryString["description"] = props.ContainsKey("video_title")
                ? props["video_title"]
                : "video desc not set in json";
            queryString["callbackUrl"] = videoIndexerCallbackUrl;
            queryString["externalId"] = alternateId;
            //queryString["metadata"] = "{string}";
            //queryString["partition"] = "{string}";

            var apiUrl = VideoIndexerApiUrl;
            var client = GetVideoIndexerHttpClient();

            // post to the API
            var result = await client.PostAsync(apiUrl + $"?{queryString}", null);

            // the JSON result in this case is the VideoIndexer assigned ID for this video.
            var json = result.Content.ReadAsStringAsync().Result;
            var videoIndexerId = JsonConvert.DeserializeObject<string>(json);
            state.VideoIndexerId = videoIndexerId;
            // save a record of this job submission
            await StoreProcessingStateRecordInCosmosAsync(state);
            // delete the breakdown
            //DeleteBreakdown();
            // don't delete for now since we need to use the VI portal to train faces
            
            return videoIndexerId;
        }

        #endregion
    }
}