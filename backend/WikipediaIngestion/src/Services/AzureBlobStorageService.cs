using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WikipediaDataIngestionFunction.Services
{
    public class AzureBlobStorageService : IStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<AzureBlobStorageService> _logger;
        
        public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger)
        {
            var connectionString = configuration["Storage__ConnectionString"];
            var containerName = configuration["Storage__ContainerName"] ?? "wikipedia-data";
            
            _containerClient = new BlobContainerClient(connectionString, containerName);
            _logger = logger;
        }
        
        public async Task SaveRawArticleAsync(WikipediaArticle article)
        {
            await EnsureContainerExistsAsync();
            
            try
            {
                var blobName = $"articles/{article.Id}.json";
                var blobClient = _containerClient.GetBlobClient(blobName);
                
                using var stream = new MemoryStream();
                await JsonSerializer.SerializeAsync(stream, article, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                stream.Position = 0;
                await blobClient.UploadAsync(stream, overwrite: true);
                
                _logger.LogDebug("Saved article {Id} to blob storage", article.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving article {Id} to blob storage", article.Id);
                throw;
            }
        }
        
        public async Task<List<WikipediaArticle>> GetRawArticlesAsync()
        {
            await EnsureContainerExistsAsync();
            
            var articles = new List<WikipediaArticle>();
            
            try
            {
                var blobs = _containerClient.GetBlobsAsync(prefix: "articles/");
                
                await foreach (var blobItem in blobs)
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    
                    using var stream = new MemoryStream();
                    await blobClient.DownloadToAsync(stream);
                    stream.Position = 0;
                    
                    var article = await JsonSerializer.DeserializeAsync<WikipediaArticle>(stream);
                    if (article != null)
                    {
                        articles.Add(article);
                    }
                }
                
                _logger.LogInformation("Retrieved {Count} articles from blob storage", articles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving articles from blob storage");
                throw;
            }
            
            return articles;
        }
        
        private async Task EnsureContainerExistsAsync()
        {
            try
            {
                await _containerClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating blob container");
                throw;
            }
        }
    }
} 