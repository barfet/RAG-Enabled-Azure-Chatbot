using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }
        
        /// <inheritdoc />
        public async Task CreateIndexIfNotExistsAsync(string indexName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(indexName, nameof(indexName));
            
            // Check if the index already exists
            using var existsRequest = new HttpRequestMessage(HttpMethod.Get, $"indexes/{indexName}?api-version={_apiVersion}");
            existsRequest.Headers.Add("api-key", _apiKey);
            
            var response = await _httpClient.SendAsync(existsRequest, cancellationToken).ConfigureAwait(false);
            
            // If index exists, return
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            
            // Create the index schema
            var indexSchema = CreateIndexSchema(indexName);
            using var content = new StringContent(
                JsonConvert.SerializeObject(indexSchema),
                Encoding.UTF8,
                "application/json");
            
            // Create the index
            using var createRequest = new HttpRequestMessage(HttpMethod.Put, $"indexes/{indexName}?api-version={_apiVersion}");
            createRequest.Headers.Add("api-key", _apiKey);
            createRequest.Content = content;
            
            response = await _httpClient.SendAsync(createRequest, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        
        /// <inheritdoc />
        public async Task UploadDocumentsAsync(
            string indexName,
            IEnumerable<ArticleChunk> chunks,
            Dictionary<string, float[]> embeddings,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(indexName, nameof(indexName));
            ArgumentNullException.ThrowIfNull(chunks, nameof(chunks));
            ArgumentNullException.ThrowIfNull(embeddings, nameof(embeddings));
            
            var chunksList = chunks.ToList();
            if (chunksList.Count == 0)
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
                    { "url", chunk.ArticleUrl.ToString() },
                    { "contentVector", embedding }
                };
                
                documents.Add(document);
            }
            
            if (documents.Count == 0)
            {
                return;
            }
            
            // Prepare the request
            var requestBody = new { value = documents };
            using var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");
                
            using var request = new HttpRequestMessage(HttpMethod.Post, $"indexes/{indexName}/docs/index?api-version={_apiVersion}");
            request.Headers.Add("api-key", _apiKey);
            request.Content = content;
            
            // Upload the documents
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        
        /// <inheritdoc />
        public async Task DeleteIndexIfExistsAsync(string indexName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(indexName, nameof(indexName));
            
            // Check if the index exists
            using var existsRequest = new HttpRequestMessage(HttpMethod.Get, $"indexes/{indexName}?api-version={_apiVersion}");
            existsRequest.Headers.Add("api-key", _apiKey);
            
            var response = await _httpClient.SendAsync(existsRequest, cancellationToken).ConfigureAwait(false);
            
            // If index doesn't exist, return
            if (!response.IsSuccessStatusCode)
            {
                return;
            }
            
            // Delete the index
            using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"indexes/{indexName}?api-version={_apiVersion}");
            deleteRequest.Headers.Add("api-key", _apiKey);
            
            response = await _httpClient.SendAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        
        /// <summary>
        /// Creates the schema for the Azure AI Search index
        /// </summary>
        /// <param name="indexName">The name of the index</param>
        /// <returns>The index schema definition</returns>
        private static object CreateIndexSchema(string indexName)
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