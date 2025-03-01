using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Infrastructure.Services
{
    /// <summary>
    /// Implementation of IEmbeddingGenerator that uses Azure OpenAI to generate embeddings
    /// </summary>
    public class AzureOpenAIEmbeddingGenerator : IEmbeddingGenerator
    {
        private readonly HttpClient _httpClient;
        private string _endpoint;
        private readonly string _deploymentName;
        private readonly string _apiKey;
        private readonly string _apiVersion;
        private readonly string _model = "text-embedding-ada-002";
        
        /// <summary>
        /// Creates a new instance of the AzureOpenAIEmbeddingGenerator
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for making requests</param>
        /// <param name="endpoint">The Azure OpenAI endpoint</param>
        /// <param name="deploymentName">The name of the embedding model deployment</param>
        /// <param name="apiKey">The API key for authentication</param>
        /// <param name="apiVersion">The API version to use</param>
        public AzureOpenAIEmbeddingGenerator(
            HttpClient httpClient,
            string endpoint,
            string deploymentName,
            string apiKey,
            string apiVersion)
        {
            _httpClient = httpClient;
            _endpoint = endpoint.TrimEnd('/');
            _deploymentName = deploymentName;
            _apiKey = apiKey;
            _apiVersion = apiVersion;
        }
        
        /// <inheritdoc />
        public async Task<Dictionary<string, float[]>> GenerateEmbeddingsAsync(
            IEnumerable<ArticleChunk> chunks, 
            CancellationToken cancellationToken = default)
        {
            var chunksList = chunks.ToList();
            if (!chunksList.Any())
            {
                return new Dictionary<string, float[]>();
            }
            
            // Ensure the endpoint is a valid URI
            if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out _))
            {
                _endpoint = $"https://{_endpoint}";
            }
            
            var requestUrl = new Uri($"{_endpoint}/openai/deployments/{_deploymentName}/embeddings?api-version={_apiVersion}");
            var result = new Dictionary<string, float[]>();
            
            // Process chunks one by one to maintain order
            for (int i = 0; i < chunksList.Count; i++)
            {
                var chunk = chunksList[i];
                
                // Create request payload
                var payload = new
                {
                    input = new[] { chunk.Content },
                    model = _model
                };
                
                var jsonContent = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Add required headers
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                request.Content = content;
                request.Headers.Add("api-key", _apiKey);
                
                // Send the request
                var response = await _httpClient.SendAsync(request, cancellationToken);
                
                // Ensure success response
                response.EnsureSuccessStatusCode();
                
                // Read and parse the response
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseJson = JsonConvert.DeserializeObject<EmbeddingResponse>(responseContent);
                
                if (responseJson?.Data != null && responseJson.Data.Any())
                {
                    // In the test, we're expecting the first chunk to have the first embedding
                    // and the second chunk to have the second embedding
                    result[chunk.Id] = responseJson.Data[0].Embedding;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Response structure from Azure OpenAI embedding API
        /// </summary>
        private class EmbeddingResponse
        {
            [JsonProperty("data")]
            public EmbeddingData[]? Data { get; set; }
        }
        
        /// <summary>
        /// Data structure for embedding response
        /// </summary>
        private class EmbeddingData
        {
            [JsonProperty("embedding")]
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }
} 