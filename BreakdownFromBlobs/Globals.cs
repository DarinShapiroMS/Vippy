using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
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

        
    }
}
