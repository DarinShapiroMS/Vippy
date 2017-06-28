using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using OrchestrationFunctions;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Linq;
using System.Linq.Expressions;

namespace VideoIndexerOrchestrationWeb
{

    
    public static class DocDbRepository<T> where T : class
    {
        private static DocumentClient client;

        public static void Initialize()
        {

            client = Globals.GetCosmosClient(Globals.ProcessingStateCosmosCollectionName);

        }

        public static async Task<IEnumerable<T>> GetItemsAsync(Expression<Func<T, bool>> predicate)
        {
            IDocumentQuery<T> query = client.CreateDocumentQuery<T>(
                UriFactory.CreateDocumentCollectionUri(Globals.CosmosDatabasename, Globals.ProcessingStateCosmosCollectionName))
                .Where(predicate)
                .AsDocumentQuery();

            List<T> results = new List<T>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<T>());
            }

            return results;
        }

        public static async Task<VideoBreakdownPOCO> GetItemDetail(string id)
        {
           

            var detail = client.CreateDocumentQuery<VideoBreakdownPOCO>(UriFactory.CreateDocumentCollectionUri(Globals.CosmosDatabasename, Globals.ProcessingStateCosmosCollectionName), "SELECT * FROM c WHERE field=value").FirstOrDefault();
            return detail;
        }
    }
}