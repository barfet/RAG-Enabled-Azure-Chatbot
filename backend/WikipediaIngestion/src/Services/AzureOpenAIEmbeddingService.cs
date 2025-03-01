using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WikipediaDataIngestionFunction.Services
{
    public class AzureOpenAIEmbeddingService : IEmbeddingService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly string _embeddingDeploymentName;
        private readonly ILogger<AzureOpenAIEmbeddingService> _logger;
        
        private const int MaxRetries = 5;
        private const int RetryDelayMs = 1000;
        
        public AzureOpenAIEmbeddingService(
            IConfiguration configuration,
            ILogger<AzureOpenAIEmbeddingService> logger)
        {
            var endpoint = configuration["OpenAI__Endpoint"];
            var apiKey = configuration["OpenAI__Key"];
            _embeddingDeploymentName = configuration["OpenAI__EmbeddingsModelDeployment"] ?? "text-embedding-ada-002";
            
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("OpenAI endpoint and API key must be provided in configuration");
            }
            
            _openAIClient = CreateClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            
            _logger = logger;
        }
        
        protected virtual OpenAIClient CreateClient(Uri endpoint, AzureKeyCredential credential)
        {
            return new OpenAIClient(endpoint, credential);
        }
        
        public async Task<List<TextChunk>> GenerateEmbeddingsAsync(List<TextChunk> chunks)
        {
            var tasks = chunks.Select(chunk => ProcessChunkAsync(chunk)).ToList();
            await Task.WhenAll(tasks);
            return chunks;
        }
        
        private async Task ProcessChunkAsync(TextChunk chunk)
        {
            try
            {
                chunk.ContentVector = await GenerateEmbeddingAsync(chunk.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding for chunk {ChunkId}", chunk.Id);
                // Don't rethrow - we want to continue processing other chunks
            }
        }
        
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    // Create embedding options with the text
                    var embeddingOptions = new EmbeddingsOptions
                    {
                        Input = { text }
                    };
                    
                    // Get embeddings
                    Response<Embeddings> response = await _openAIClient.GetEmbeddingsAsync(
                        _embeddingDeploymentName, embeddingOptions);
                    
                    if (response.Value.Data.Count > 0)
                    {
                        return response.Value.Data[0].Embedding.ToArray();
                    }
                    
                    throw new Exception("Embedding response did not contain expected data");
                }
                catch (RequestFailedException ex) when (ex.Status == 429) // Too Many Requests
                {
                    if (attempt < MaxRetries)
                    {
                        _logger.LogWarning("Rate limit hit, retrying in {Delay}ms (Attempt {Attempt}/{MaxRetries})", RetryDelayMs * attempt, attempt, MaxRetries);
                        await Task.Delay(RetryDelayMs * attempt);
                    }
                    else
                    {
                        _logger.LogError(ex, "Failed to generate embedding after {MaxRetries} attempts due to rate limiting", MaxRetries);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating embedding (Attempt {Attempt}/{MaxRetries})", attempt, MaxRetries);
                    
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(RetryDelayMs * attempt);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            
            throw new Exception($"Failed to generate embedding after {MaxRetries} attempts");
        }
    }
} 