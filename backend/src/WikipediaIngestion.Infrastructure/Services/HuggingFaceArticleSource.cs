using System.Text.Json;
using Newtonsoft.Json;
using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Infrastructure.Services
{
    /// <summary>
    /// Implementation of IArticleSource that fetches Wikipedia articles from the Hugging Face API
    /// </summary>
    public class HuggingFaceArticleSource : IArticleSource
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://datasets-api.huggingface.co/datasets/wikimedia/wikipedia";
        
        public HuggingFaceArticleSource(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
        }
        
        /// <inheritdoc />
        public async Task<IEnumerable<WikipediaArticle>> GetArticlesAsync(int limit, int offset, CancellationToken cancellationToken = default)
        {
            // Prepare the request URL with query parameters
            var requestUrl = $"{_baseUrl}?limit={limit}&offset={offset}";
            
            // Prepare the request with authorization header
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            
            // Send the request
            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            // Ensure we got a successful response
            response.EnsureSuccessStatusCode();
            
            // Get the response content
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Parse the JSON response using Newtonsoft.Json
            var articleDtos = JsonConvert.DeserializeObject<WikipediaArticleDto[]>(content);
            
            if (articleDtos == null || !articleDtos.Any())
            {
                return Enumerable.Empty<WikipediaArticle>();
            }
            
            // Convert DTOs to domain models
            return articleDtos.Select(dto => new WikipediaArticle
            {
                Id = dto.Id,
                Title = dto.Title,
                Content = dto.Text,
                Url = dto.Url,
                LastUpdated = DateTime.Parse(dto.Timestamp).ToUniversalTime(),
                Categories = dto.Categories?.ToList() ?? new List<string>()
            }).ToList();
        }
        
        /// <summary>
        /// Data transfer object for Wikipedia article information from Hugging Face API
        /// </summary>
        private class WikipediaArticleDto
        {
            [JsonProperty("id")]
            public required string Id { get; set; }
            
            [JsonProperty("title")]
            public required string Title { get; set; }
            
            [JsonProperty("text")]
            public required string Text { get; set; }
            
            [JsonProperty("url")]
            public required string Url { get; set; }
            
            [JsonProperty("timestamp")]
            public required string Timestamp { get; set; }
            
            [JsonProperty("categories")]
            public required List<string> Categories { get; set; } = new();
        }
    }
} 