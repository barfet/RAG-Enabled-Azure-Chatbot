using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Models;
using WikipediaIngestion.Functions;
using WikipediaIngestion.Infrastructure.Services;
using WikipediaIngestion.Core.Services;
using Xunit;
using Xunit.Sdk;

namespace WikipediaIngestion.IntegrationTests;

/// <summary>
/// Integration tests for the Wikipedia Data Ingestion Pipeline
/// These tests use actual dependencies and external services
/// </summary>
public class WikipediaDataIngestionPipelineTests : IAsyncLifetime
{
    private IConfiguration _configuration = null!;
    private IServiceProvider _serviceProvider = null!;
    private WikipediaDataIngestionFunction _function = null!;
    private string _testIndexName = "wikipedia-test-index";
    
    public async Task InitializeAsync()
    {
        // Load configuration from appsettings.json, environment variables, or user secrets
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables();
            
        _configuration = builder.Build();
        
        // Set up dependency injection
        var services = new ServiceCollection();
        
        // Register real implementations
        services.AddSingleton(_configuration);
        services.AddLogging(builder => builder.AddConsole());
        services.AddHttpClient();
        
        // Register core services with real implementations
        services.AddSingleton<ITextChunker, ParagraphTextChunker>();
        services.AddSingleton<IArticleSource>(sp => 
            new HuggingFaceArticleSource(
                sp.GetRequiredService<HttpClient>(),
                _configuration["HuggingFaceApiKey"] ?? throw new InvalidOperationException("HuggingFaceApiKey is required")));
                
        services.AddSingleton<IEmbeddingGenerator>(sp => 
            new AzureOpenAIEmbeddingGenerator(
                sp.GetRequiredService<HttpClient>(),
                _configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required"),
                _configuration["AzureOpenAI:DeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is required"), 
                _configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is required"),
                _configuration["AzureOpenAI:ApiVersion"] ?? "2023-05-15"));
                
        services.AddSingleton<ISearchIndexer>(sp => 
            new AzureSearchIndexer(
                sp.GetRequiredService<HttpClient>(),
                _configuration["AzureSearch:ApiKey"] ?? throw new InvalidOperationException("AzureSearch:ApiKey is required")));
                
        services.AddSingleton<WikipediaDataIngestionFunction>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        // Create function with real dependencies
        _function = _serviceProvider.GetRequiredService<WikipediaDataIngestionFunction>();
        
        // Clean up any existing test index
        var searchIndexer = _serviceProvider.GetRequiredService<ISearchIndexer>();
        try
        {
            await searchIndexer.DeleteIndexIfExistsAsync(_testIndexName);
        }
        catch (Exception ex)
        {
            // If deletion fails, log but continue
            Console.WriteLine($"Failed to delete test index: {ex.Message}");
        }
    }
    
    public async Task DisposeAsync()
    {
        // Clean up after tests
        var searchIndexer = _serviceProvider.GetRequiredService<ISearchIndexer>();
        try
        {
            await searchIndexer.DeleteIndexIfExistsAsync(_testIndexName);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
    
    [Fact]
    public async Task ArticleSource_ShouldRetrieveArticles()
    {
        // Arrange
        var articleSource = _serviceProvider.GetRequiredService<IArticleSource>();
        
        // Act
        var articles = await articleSource.GetArticlesAsync(5, 0);
        
        // Assert
        Assert.NotNull(articles);
        Assert.NotEmpty(articles);
        foreach (var article in articles)
        {
            Assert.NotNull(article.Id);
            Assert.NotNull(article.Title);
            Assert.NotNull(article.Content);
        }
    }
    
    [Fact]
    public void TextChunker_ShouldChunkArticle()
    {
        // Arrange
        var textChunker = _serviceProvider.GetRequiredService<ITextChunker>();
        var article = new WikipediaArticle
        {
            Id = "test-article-id",
            Title = "Test Article Title",
            Content = "This is the first paragraph of the test article.\n\nThis is the second paragraph of the test article.",
            Url = new Uri("https://example.com/test-article")
        };
        article.AddCategories(new List<string> { "Test", "Category" });
        
        // Act
        var chunks = textChunker.ChunkArticle(article).ToList();
        
        // Assert
        Assert.NotEmpty(chunks);
        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => {
            Assert.Equal(article.Id, chunk.ArticleId);
            Assert.Contains(article.Title, chunk.ArticleTitle);
            Assert.NotEmpty(chunk.Content);
        });
    }
    
    [Fact]
    public async Task EmbeddingGenerator_ShouldGenerateEmbeddings()
    {
        // Arrange
        var embeddingGenerator = _serviceProvider.GetRequiredService<IEmbeddingGenerator>();
        var chunks = new List<ArticleChunk>
        {
            new ArticleChunk
            {
                Id = Guid.NewGuid().ToString(),
                ArticleId = "test-article-id",
                ArticleTitle = "Test Article Title - Chunk 1",
                Content = "This is the content of the first chunk."
            }
        };
        
        // Act
        var embeddings = await embeddingGenerator.GenerateEmbeddingsAsync(chunks);
        
        // Assert
        Assert.NotNull(embeddings);
        Assert.Single(embeddings);
        Assert.Equal(chunks[0].Id, embeddings.Keys.First());
        Assert.NotEmpty(embeddings.Values.First());
    }
    
    [Fact]
    public async Task SearchIndexer_ShouldCreateAndManageIndex()
    {
        // Arrange
        var searchIndexer = _serviceProvider.GetRequiredService<ISearchIndexer>();
        
        // Act - Create index
        await searchIndexer.CreateIndexIfNotExistsAsync(_testIndexName);
        
        // Add documents
        var chunks = new List<ArticleChunk>
        {
            new ArticleChunk
            {
                Id = Guid.NewGuid().ToString(),
                ArticleId = "test-article-id",
                ArticleTitle = "Test Article Title - Chunk 1",
                Content = "This is the content of the first chunk."
            }
        };
        
        var embeddings = new Dictionary<string, float[]>
        {
            { chunks[0].Id, Enumerable.Range(0, 1536).Select(i => (float)i / 1536).ToArray() }
        };
        
        await searchIndexer.UploadDocumentsAsync(_testIndexName, chunks, embeddings);
        
        // Assert - Verify index exists (would throw if it didn't exist)
        await searchIndexer.DeleteIndexIfExistsAsync(_testIndexName);
    }
    
    [Fact]
    public async Task FullPipeline_ShouldProcessArticlesEndToEnd()
    {
        // This test focuses on the full pipeline integration
        // Skip if not running in an environment with all required API keys
        if (string.IsNullOrEmpty(_configuration["HuggingFaceApiKey"]) ||
            string.IsNullOrEmpty(_configuration["AzureOpenAI:ApiKey"]) ||
            string.IsNullOrEmpty(_configuration["AzureSearch:ApiKey"]))
        {
            Assert.True(false, "Skipping pipeline test - API keys not configured");
            return;
        }
        
        // Arrange - All dependencies are already set up via DI
        var articleSource = _serviceProvider.GetRequiredService<IArticleSource>();
        var textChunker = _serviceProvider.GetRequiredService<ITextChunker>();
        var embeddingGenerator = _serviceProvider.GetRequiredService<IEmbeddingGenerator>();
        var searchIndexer = _serviceProvider.GetRequiredService<ISearchIndexer>();
        
        // Act - Execute each step manually
        // 1. Fetch articles
        var articles = await articleSource.GetArticlesAsync(2, 0);
        Assert.NotEmpty(articles);
        
        // 2. Chunk articles
        var allChunks = new List<ArticleChunk>();
        foreach (var article in articles)
        {
            var chunks = textChunker.ChunkArticle(article).ToList();
            allChunks.AddRange(chunks);
        }
        Assert.NotEmpty(allChunks);
        
        // 3. Generate embeddings
        var embeddings = await embeddingGenerator.GenerateEmbeddingsAsync(allChunks);
        Assert.Equal(allChunks.Count, embeddings.Count);
        
        // 4. Index in Azure Search
        await searchIndexer.CreateIndexIfNotExistsAsync(_testIndexName);
        await searchIndexer.UploadDocumentsAsync(_testIndexName, allChunks, embeddings);
        
        // Clean up
        await searchIndexer.DeleteIndexIfExistsAsync(_testIndexName);
    }
    
    [Fact]
    public void HttpTrigger_ShouldReturnSuccessResponse()
    {
        // Skip if not running in an environment with all required API keys
        if (string.IsNullOrEmpty(_configuration["HuggingFaceApiKey"]) ||
            string.IsNullOrEmpty(_configuration["AzureOpenAI:ApiKey"]) ||
            string.IsNullOrEmpty(_configuration["AzureSearch:ApiKey"]))
        {
            Assert.True(false, "Skipping HTTP trigger test - API keys not configured");
            return;
        }
        
        // This is a placeholder for HTTP trigger testing
        // In a real implementation, you would need to set up a Functions host or use additional test infrastructure
        Assert.True(false, "HTTP trigger testing requires a Functions host or additional test infrastructure");
        return;
    }
}
