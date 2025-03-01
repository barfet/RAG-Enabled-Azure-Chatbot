using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.ApplicationInsights.Extensibility;
using WikipediaDataIngestionFunction.Services;
using System.Net.Http.Headers;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Register services
        services.AddApplicationInsightsTelemetryWorkerService();
        services.AddSingleton<ITelemetryInitializer, CloudRoleNameTelemetryInitializer>();
        
        // Register HttpClient for Wikipedia API
        services.AddHttpClient("WikipediaClient", client =>
        {
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("RAGEnabledChatbot", "1.0"));
        });
        
        // Register core services
        services.AddSingleton<IWikipediaService, WikipediaService>();
        services.AddSingleton<ITextProcessingService, TextProcessingService>();
        services.AddSingleton<IEmbeddingService, AzureOpenAIEmbeddingService>();
        services.AddSingleton<ISearchIndexService, AzureSearchIndexService>();
        services.AddSingleton<IStorageService, AzureBlobStorageService>();
    })
    .Build();

host.Run();

// Telemetry initializer class to set cloud role name
public class CloudRoleNameTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(Microsoft.ApplicationInsights.Channel.ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = "WikipediaDataIngestion";
    }
} 