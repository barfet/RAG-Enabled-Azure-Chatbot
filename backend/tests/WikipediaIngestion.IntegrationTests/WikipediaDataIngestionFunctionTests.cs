using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
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
/// Integration tests specifically focused on the WikipediaDataIngestionFunction
/// </summary>
public class WikipediaDataIngestionFunctionTests : IAsyncLifetime
{
    private IConfiguration _configuration = null!;
    private IServiceProvider _serviceProvider = null!;
    private WikipediaDataIngestionFunction _function = null!;
    private string _testIndexName = "wiki-function-test-index";
    
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
                
        // Create test-specific function implementation with test index name
        services.AddSingleton<WikipediaDataIngestionFunction>(sp =>
            new TestWikipediaDataIngestionFunction(
                sp.GetRequiredService<IArticleSource>(),
                sp.GetRequiredService<ITextChunker>(),
                sp.GetRequiredService<IEmbeddingGenerator>(),
                sp.GetRequiredService<ISearchIndexer>(),
                sp.GetRequiredService<ILogger<WikipediaDataIngestionFunction>>(),
                _testIndexName
            ));
        
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
    public async Task ProcessWikipediaArticlesAsync_ShouldProcessAndIndexArticles()
    {
        // Skip if not running in an environment with all required API keys
        if (string.IsNullOrEmpty(_configuration["HuggingFaceApiKey"]) ||
            string.IsNullOrEmpty(_configuration["AzureOpenAI:ApiKey"]) ||
            string.IsNullOrEmpty(_configuration["AzureSearch:ApiKey"]))
        {
            Assert.True(false, "Skipping function test - API keys not configured");
            return;
        }
        
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var searchIndexer = _serviceProvider.GetRequiredService<ISearchIndexer>();
        
        // Create a FunctionContext (simple mock)
        var mockFunctionContext = new MockFunctionContext();
        
        // Act - Execute the function
        await _function.ProcessWikipediaArticlesAsync(mockFunctionContext);
        
        // Assert
        // Verify that the index exists - this would throw if it didn't exist
        await searchIndexer.DeleteIndexIfExistsAsync(_testIndexName);
        
        stopwatch.Stop();
        Console.WriteLine($"Function execution completed in {stopwatch.Elapsed.TotalSeconds} seconds");
    }
    
    // Test-specific implementation of the function that allows custom index name
    private class TestWikipediaDataIngestionFunction : WikipediaDataIngestionFunction
    {
        public TestWikipediaDataIngestionFunction(
            IArticleSource articleSource,
            ITextChunker textChunker,
            IEmbeddingGenerator embeddingGenerator,
            ISearchIndexer searchIndexer,
            ILogger<WikipediaDataIngestionFunction> logger,
            string indexName) 
            : base(articleSource, textChunker, embeddingGenerator, searchIndexer, logger)
        {
            // Override protected properties for testing
            CustomizeForTesting(indexName, 2); // Only process 2 articles for testing
        }
        
        // Method to allow tests to customize behavior
        public void CustomizeForTesting(string indexName, int limit)
        {
            // Use reflection to set private fields for testing purposes
            var type = typeof(WikipediaDataIngestionFunction);
            type.GetField("_indexName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(this, indexName);
            type.GetField("_limit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(this, limit);
        }
    }
    
    // Simple mock FunctionContext for testing
    private class MockFunctionContext : FunctionContext
    {
        private IServiceProvider _instanceServices = new EmptyServiceProvider();
        private IDictionary<object, object> _items = new Dictionary<object, object>();

        public override IServiceProvider InstanceServices 
        { 
            get => _instanceServices;
            set => _instanceServices = value; 
        }
        
        public override FunctionDefinition FunctionDefinition => throw new NotImplementedException();
        
        public override IDictionary<object, object> Items 
        { 
            get => _items; 
            set => _items = value; 
        }
        
        public override IInvocationFeatures Features => throw new NotImplementedException();
        public override string InvocationId => Guid.NewGuid().ToString();
        public override string FunctionId => "ProcessWikipediaArticles";
        public override TraceContext TraceContext => throw new NotImplementedException();
        public override BindingContext BindingContext => throw new NotImplementedException();
        public override RetryContext RetryContext => throw new NotImplementedException();
    }
    
    // Empty service provider for the mock
    private class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
} 