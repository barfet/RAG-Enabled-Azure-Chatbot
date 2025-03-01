using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WikipediaDataIngestionFunction.Services
{
    public class AzureSearchIndexService : ISearchIndexService
    {
        private readonly SearchIndexClient _searchIndexClient;
        private readonly SearchClient _searchClient;
        private readonly string _indexName;
        private readonly ILogger<AzureSearchIndexService> _logger;
        
        public AzureSearchIndexService(IConfiguration configuration, ILogger<AzureSearchIndexService> logger)
        {
            var endpoint = new Uri(configuration["Search__Endpoint"]);
            var adminKey = configuration["Search__Key"];
            _indexName = configuration["Search__IndexName"] ?? "wikipedia-index";
            
            _searchIndexClient = new SearchIndexClient(endpoint, new AzureKeyCredential(adminKey));
            _searchClient = new SearchClient(endpoint, _indexName, new AzureKeyCredential(adminKey));
            _logger = logger;
        }
        
        public async Task CreateIndexIfNotExistsAsync()
        {
            _logger.LogInformation("Checking if index {IndexName} exists", _indexName);
            
            try
            {
                var getIndexResponse = await _searchIndexClient.GetIndexAsync(_indexName);
                _logger.LogInformation("Index {IndexName} already exists", _indexName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("Creating new index {IndexName}", _indexName);
                
                var fieldBuilder = new FieldBuilder();
                var searchFields = fieldBuilder.Build(typeof(TextChunkIndex));
                
                var definition = new SearchIndex(_indexName, searchFields)
                {
                    VectorSearch = new VectorSearch
                    {
                        Algorithms =
                        {
                            new HnswAlgorithmConfiguration("my-vector-config")
                            {
                                Parameters = new HnswParameters
                                {
                                    M = 4,
                                    EfConstruction = 400,
                                    EfSearch = 500,
                                    Metric = VectorSearchAlgorithmMetric.Cosine
                                }
                            }
                        }
                    },
                    SemanticSearch = new SemanticSearch
                    {
                        Configurations =
                        {
                            new SemanticConfiguration("my-semantic-config", new SemanticPrioritizedFields
                            {
                                TitleField = new SemanticField("title"),
                                ContentFields = { new SemanticField("content") },
                                KeywordsFields = { new SemanticField("categories") }
                            })
                        }
                    }
                };
                
                var response = await _searchIndexClient.CreateOrUpdateIndexAsync(definition);
                _logger.LogInformation("Index {IndexName} created successfully", _indexName);
            }
        }
        
        public async Task IndexChunksAsync(List<TextChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0)
            {
                _logger.LogWarning("No chunks to index");
                return;
            }
            
            _logger.LogInformation("Indexing {Count} chunks to Azure AI Search", chunks.Count);
            
            try
            {
                // Convert chunks to search documents
                var documents = chunks.Select(chunk => new TextChunkIndex
                {
                    Id = chunk.Id,
                    Title = chunk.Title,
                    Content = chunk.Content,
                    Section = chunk.Section,
                    Url = chunk.Url,
                    LastUpdated = chunk.LastUpdated,
                    Categories = chunk.Categories.ToArray(),
                    ContentVector = chunk.ContentVector
                }).ToList();
                
                // Index in batches to handle Azure Search limits
                const int batchSize = 1000;
                for (int i = 0; i < documents.Count; i += batchSize)
                {
                    var batch = documents.Skip(i).Take(batchSize).ToList();
                    _logger.LogInformation("Indexing batch {BatchNumber} with {BatchSize} documents", i / batchSize + 1, batch.Count);
                    
                    var response = await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(batch));
                    
                    // Check for errors
                    if (response.Value.Results.Any(r => r.Succeeded == false))
                    {
                        foreach (var result in response.Value.Results.Where(r => r.Succeeded == false))
                        {
                            _logger.LogError("Failed to index document {Key}: {ErrorMessage}", 
                                result.Key, result.ErrorMessage);
                        }
                    }
                }
                
                _logger.LogInformation("Successfully indexed {Count} chunks to Azure AI Search", chunks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing chunks to Azure AI Search");
                throw;
            }
        }
        
        // Index document class that matches the search index schema
        private class TextChunkIndex
        {
            [SimpleField(IsKey = true, IsFilterable = true)]
            public string Id { get; set; } = string.Empty;
            
            [SearchableField(IsFilterable = true, IsSortable = true)]
            public string Title { get; set; } = string.Empty;
            
            [SearchableField]
            public string Content { get; set; } = string.Empty;
            
            [SearchableField(IsFilterable = true)]
            public string Section { get; set; } = string.Empty;
            
            [SimpleField]
            public string Url { get; set; } = string.Empty;
            
            [SimpleField(IsFilterable = true, IsSortable = true)]
            public DateTime LastUpdated { get; set; }
            
            [SearchableField(IsFilterable = true, IsFacetable = true)]
            public string[] Categories { get; set; } = Array.Empty<string>();
            
            [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "my-vector-config")]
            public float[] ContentVector { get; set; } = Array.Empty<float>();
        }
    }
} 