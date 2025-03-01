using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }
        
        /// <inheritdoc />
        public async Task<IEnumerable<WikipediaArticle>> GetArticlesAsync(int limit, int offset, CancellationToken cancellationToken = default)
        {
            // Prepare the request URL with query parameters
            var requestUrl = $"{_baseUrl}?limit={limit}&offset={offset}";
            
            // Prepare the request with authorization header
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            
            // Send the request
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            
            // Ensure we got a successful response
            response.EnsureSuccessStatusCode();
            
            // Get the response content
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            
            // Parse the JSON response using Newtonsoft.Json
            var articleDtos = JsonConvert.DeserializeObject<WikipediaArticleDto[]>(content);
            
            if (articleDtos == null || articleDtos.Length == 0)
            {
                return Enumerable.Empty<WikipediaArticle>();
            }
            
            // Convert DTOs to domain models
            var results = new List<WikipediaArticle>();
            
            foreach (var dto in articleDtos)
            {
                var article = new WikipediaArticle
                {
                    Id = dto.Id,
                    Title = dto.Title,
                    Content = dto.Text,
                    Url = new Uri(dto.Url),
                    LastUpdated = DateTime.Parse(dto.Timestamp, CultureInfo.InvariantCulture).ToUniversalTime()
                };
                
                // Add categories using the AddCategories method
                if (dto.Categories != null && dto.Categories.Count > 0)
                {
                    article.AddCategories(dto.Categories);
                }
                
                results.Add(article);
            }
            
            return results;
        }
        
        /// <summary>
        /// Data transfer object for Wikipedia article information from Hugging Face API
        /// </summary>
        private sealed class WikipediaArticleDto
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
            public List<string>? Categories { get; set; }
        }
    }
} 