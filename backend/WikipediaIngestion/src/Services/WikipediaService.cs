using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WikipediaDataIngestionFunction.Services
{
    public class WikipediaService : IWikipediaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WikipediaService> _logger;
        private readonly int _maxArticles;

        // Hugging Face API endpoint for Wikipedia dataset
        private const string HF_API_URL = "https://datasets-server.huggingface.co/rows";
        private const string DATASET_ID = "wikimedia/wikipedia";
        private const string SUBSET = "20231101.en"; // English Wikipedia snapshot
        
        public WikipediaService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<WikipediaService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("WikipediaClient");
            _logger = logger;
            _maxArticles = int.Parse(config["Wikipedia__MaxArticlesToProcess"] ?? "1000");
        }

        public async Task<List<WikipediaArticle>> GetArticlesAsync(int maxArticles)
        {
            _logger.LogInformation("Fetching {Count} Wikipedia articles", maxArticles);
            var articles = new List<WikipediaArticle>();
            
            try
            {
                // For demo purposes, we'll get data in smaller batches
                int batchSize = 100;
                int offset = 0;
                
                while (articles.Count < maxArticles)
                {
                    var requestUri = $"{HF_API_URL}?dataset={DATASET_ID}&config={SUBSET}&split=train&offset={offset}&limit={batchSize}";
                    var response = await _httpClient.GetAsync(requestUri);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to fetch Wikipedia articles: {StatusCode}", response.StatusCode);
                        break;
                    }
                    
                    var responseContent = await response.Content.ReadFromJsonAsync<HuggingFaceResponse>();
                    
                    if (responseContent == null || responseContent.Rows.Count == 0)
                    {
                        _logger.LogInformation("No more articles to fetch");
                        break;
                    }
                    
                    foreach (var row in responseContent.Rows)
                    {
                        if (articles.Count >= maxArticles) break;
                        
                        var article = new WikipediaArticle
                        {
                            Id = Guid.NewGuid().ToString(),
                            Title = row.Row.Title ?? "Unknown Title",
                            Content = row.Row.Text ?? "",
                            Url = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(row.Row.Title ?? "")}", 
                            LastUpdated = DateTime.UtcNow, // Actual last update not available in this API
                            Categories = row.Row.Categories?.Split('|').ToList() ?? new List<string>()
                        };
                        
                        articles.Add(article);
                    }
                    
                    offset += responseContent.Rows.Count;
                    _logger.LogInformation("Fetched {Count} articles so far", articles.Count);
                    
                    // Add a slight delay to avoid hitting API rate limits
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Wikipedia articles");
            }
            
            return articles;
        }
        
        // Classes for deserializing Hugging Face API response
        private class HuggingFaceResponse
        {
            public List<HuggingFaceRow> Rows { get; set; } = new List<HuggingFaceRow>();
        }
        
        private class HuggingFaceRow
        {
            public WikiRow Row { get; set; } = new WikiRow();
        }
        
        private class WikiRow
        {
            public string? Title { get; set; }
            public string? Text { get; set; }
            public string? Categories { get; set; }
        }
    }
} 