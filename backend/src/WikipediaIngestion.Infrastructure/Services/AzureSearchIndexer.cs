using System.Text;
using Newtonsoft.Json;
using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Infrastructure.Services
{
    /// <summary>
    /// Implementation of ISearchIndexer that uses Azure AI Search
    /// </summary>
    public class AzureSearchIndexer : ISearchIndexer
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiVersion = "2023-07-01-Preview"; // Version that supports vector search
        
        /// <summary>
        /// Creates a new instance of AzureSearchIndexer
        /// </summary>
        /// <param name="httpClient">HTTP client for making requests to Azure AI Search</param>
        /// <param name="apiKey">API key for Azure AI Search</param>
        public AzureSearchIndexer(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
        }
        
        /// <inheritdoc />
        public async Task CreateIndexIfNotExistsAsync(string indexName, CancellationToken cancellationToken = default)
        {
            // Check if the index already exists
            var request = new HttpRequestMessage(HttpMethod.Get, $"indexes/{indexName}?api-version={_apiVersion}");
            request.Headers.Add("api-key", _apiKey);
            
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            // If index exists, return
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            
            // Create the index schema
            var indexSchema = CreateIndexSchema(indexName);
            var content = new StringContent(
                JsonConvert.SerializeObject(indexSchema),
                Encoding.UTF8,
                "application/json");
            
            // Create the index
            request = new HttpRequestMessage(HttpMethod.Put, $"indexes/{indexName}?api-version={_apiVersion}");
            request.Headers.Add("api-key", _apiKey);
            request.Content = content;
            
            response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        
        /// <inheritdoc />
        public async Task UploadDocumentsAsync(
            string indexName,
            IEnumerable<ArticleChunk> chunks,
            Dictionary<string, float[]> embeddings,
            CancellationToken cancellationToken = default)
        {
            var chunksList = chunks.ToList();
            if (!chunksList.Any())
            {
                return;
            }
            
            // Prepare the documents
            var documents = new List<Dictionary<string, object>>();
            
            foreach (var chunk in chunksList)
            {
                if (!embeddings.TryGetValue(chunk.Id, out var embedding))
                {
                    continue; // Skip chunks without embeddings
                }
                
                var document = new Dictionary<string, object>
                {
                    { "id", chunk.Id },
                    { "articleId", chunk.ArticleId },
                    { "title", chunk.ArticleTitle },
                    { "content", chunk.Content },
                    { "section", chunk.SectionTitle },
                    { "url", chunk.ArticleUrl },
                    { "contentVector", embedding }
                };
                
                documents.Add(document);
            }
            
            if (!documents.Any())
            {
                return;
            }
            
            // Prepare the request
            var requestBody = new { value = documents };
            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");
                
            var request = new HttpRequestMessage(HttpMethod.Post, $"indexes/{indexName}/docs/index?api-version={_apiVersion}");
            request.Headers.Add("api-key", _apiKey);
            request.Content = content;
            
            // Upload the documents
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        
        /// <summary>
        /// Creates the schema for the Azure AI Search index
        /// </summary>
        /// <param name="indexName">The name of the index</param>
        /// <returns>The index schema definition</returns>
        private object CreateIndexSchema(string indexName)
        {
            return new
            {
                name = indexName,
                fields = new object[]
                {
                    new { name = "id", type = "Edm.String", key = true, filterable = true },
                    new { name = "articleId", type = "Edm.String", filterable = true },
                    new { name = "title", type = "Edm.String", searchable = true, retrievable = true, filterable = true, sortable = true },
                    new { name = "content", type = "Edm.String", searchable = true, retrievable = true },
                    new { name = "section", type = "Edm.String", searchable = true, retrievable = true, filterable = true },
                    new { name = "url", type = "Edm.String", retrievable = true },
                    new 
                    { 
                        name = "contentVector", 
                        type = "Collection(Edm.Single)", 
                        dimensions = 1536, // Dimensions for text-embedding-ada-002
                        vectorSearchConfiguration = "my-vector-config" 
                    }
                },
                vectorSearch = new
                {
                    algorithmConfigurations = new object[]
                    {
                        new
                        {
                            name = "my-vector-config",
                            kind = "hnsw",
                            parameters = new
                            {
                                m = 4,
                                efConstruction = 400,
                                efSearch = 500,
                                metric = "cosine"
                            }
                        }
                    }
                },
                semantic = new
                {
                    configurations = new object[]
                    {
                        new
                        {
                            name = "my-semantic-config",
                            prioritizedFields = new
                            {
                                titleField = new { fieldName = "title" },
                                contentFields = new object[] { new { fieldName = "content" } },
                                keywordsFields = new object[] { new { fieldName = "section" } }
                            }
                        }
                    }
                }
            };
        }
    }
} 