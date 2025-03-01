using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Services;
using WikipediaIngestion.Infrastructure.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Add configuration
        var configuration = context.Configuration;
        
        // Register domain services
        services.AddSingleton<ITextChunker, ParagraphTextChunker>();
        
        // Register infrastructure services
        services.AddHttpClient();
        
        // Configure HuggingFaceArticleSource
        services.AddSingleton<IArticleSource>(sp => 
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var apiKey = configuration["HuggingFace:ApiKey"] ?? throw new InvalidOperationException("HuggingFace:ApiKey is not configured");
            return new HuggingFaceArticleSource(httpClient, apiKey);
        });
        
        // Configure AzureOpenAIEmbeddingGenerator
        services.AddSingleton<IEmbeddingGenerator>(sp => 
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured");
            var deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is not configured");
            var apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not configured");
            var apiVersion = configuration["AzureOpenAI:ApiVersion"] ?? "2023-12-01-preview";
            
            return new AzureOpenAIEmbeddingGenerator(httpClient, endpoint, deploymentName, apiKey, apiVersion);
        });
        
        // Configure AzureSearchIndexer
        services.AddSingleton<ISearchIndexer>(sp => 
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var endpoint = configuration["AzureSearch:Endpoint"] ?? throw new InvalidOperationException("AzureSearch:Endpoint is not configured");
            var apiKey = configuration["AzureSearch:ApiKey"] ?? throw new InvalidOperationException("AzureSearch:ApiKey is not configured");
            
            httpClient.BaseAddress = new Uri(endpoint);
            
            return new AzureSearchIndexer(httpClient, apiKey);
        });
    })
    .Build();

host.Run();
