using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WikipediaDataIngestionFunction.Services;
using Xunit;
using FluentAssertions;

namespace WikipediaDataIngestionFunction.Tests.Configuration
{
    public class ConfigurationTests
    {
        [Fact]
        public void WikipediaService_UsesCorrectConfigurationSettings()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<WikipediaService>>();
            
            // Setup expected configuration values
            configMock.Setup(c => c["HuggingFace:EndpointUrl"]).Returns("https://test-endpoint.com");
            configMock.Setup(c => c["HuggingFace:Dataset"]).Returns("test-dataset");
            configMock.Setup(c => c["HuggingFace:MaxArticles"]).Returns("5");
            
            // Act
            var service = new WikipediaService(configMock.Object, Mock.Of<IHttpClientFactory>(), loggerMock.Object);
            
            // Assert - Testing private fields requires reflection
            var endpointUrlField = typeof(WikipediaService).GetField("_endpointUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var datasetField = typeof(WikipediaService).GetField("_dataset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxArticlesField = typeof(WikipediaService).GetField("_maxArticles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            endpointUrlField.GetValue(service).Should().Be("https://test-endpoint.com");
            datasetField.GetValue(service).Should().Be("test-dataset");
            maxArticlesField.GetValue(service).Should().Be(5);
            
            // Verify configuration was accessed
            configMock.Verify(c => c["HuggingFace:EndpointUrl"], Times.Once);
            configMock.Verify(c => c["HuggingFace:Dataset"], Times.Once);
            configMock.Verify(c => c["HuggingFace:MaxArticles"], Times.Once);
        }
        
        [Fact]
        public void AzureOpenAIEmbeddingService_UsesCorrectConfigurationSettings()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AzureOpenAIEmbeddingService>>();
            
            // Setup expected configuration values
            configMock.Setup(c => c["AzureOpenAI:Endpoint"]).Returns("https://openai-test.openai.azure.com");
            configMock.Setup(c => c["AzureOpenAI:Key"]).Returns("test-key");
            configMock.Setup(c => c["AzureOpenAI:EmbeddingDeployment"]).Returns("text-embedding-ada-002");
            
            // Act
            var service = new AzureOpenAIEmbeddingService(configMock.Object, loggerMock.Object);
            
            // Assert - Testing private fields requires reflection
            var endpointField = typeof(AzureOpenAIEmbeddingService).GetField("_endpoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var keyField = typeof(AzureOpenAIEmbeddingService).GetField("_key", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var deploymentField = typeof(AzureOpenAIEmbeddingService).GetField("_embeddingDeployment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            endpointField.GetValue(service).Should().Be("https://openai-test.openai.azure.com");
            keyField.GetValue(service).Should().Be("test-key");
            deploymentField.GetValue(service).Should().Be("text-embedding-ada-002");
            
            // Verify configuration was accessed
            configMock.Verify(c => c["AzureOpenAI:Endpoint"], Times.Once);
            configMock.Verify(c => c["AzureOpenAI:Key"], Times.Once);
            configMock.Verify(c => c["AzureOpenAI:EmbeddingDeployment"], Times.Once);
        }
        
        [Fact]
        public void AzureSearchIndexService_UsesCorrectConfigurationSettings()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AzureSearchIndexService>>();
            
            // Setup expected configuration values
            configMock.Setup(c => c["AzureSearch:Endpoint"]).Returns("https://search-test.search.windows.net");
            configMock.Setup(c => c["AzureSearch:Key"]).Returns("test-search-key");
            configMock.Setup(c => c["AzureSearch:IndexName"]).Returns("wikipedia-index");
            
            // Act
            var service = new AzureSearchIndexService(configMock.Object, loggerMock.Object);
            
            // Assert - Testing private fields requires reflection
            var endpointField = typeof(AzureSearchIndexService).GetField("_searchEndpoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var keyField = typeof(AzureSearchIndexService).GetField("_searchKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var indexNameField = typeof(AzureSearchIndexService).GetField("_indexName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            endpointField.GetValue(service).Should().Be("https://search-test.search.windows.net");
            keyField.GetValue(service).Should().Be("test-search-key");
            indexNameField.GetValue(service).Should().Be("wikipedia-index");
            
            // Verify configuration was accessed
            configMock.Verify(c => c["AzureSearch:Endpoint"], Times.Once);
            configMock.Verify(c => c["AzureSearch:Key"], Times.Once);
            configMock.Verify(c => c["AzureSearch:IndexName"], Times.Once);
        }
        
        [Fact]
        public void AzureBlobStorageService_UsesCorrectConfigurationSettings()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AzureBlobStorageService>>();
            
            // Setup expected configuration values
            configMock.Setup(c => c["Storage:ConnectionString"]).Returns("DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=test-key;EndpointSuffix=core.windows.net");
            configMock.Setup(c => c["Storage:ContainerName"]).Returns("wikipedia-data");
            
            // Act
            var service = new AzureBlobStorageService(configMock.Object, loggerMock.Object);
            
            // Assert - Testing private fields requires reflection
            var connectionStringField = typeof(AzureBlobStorageService).GetField("_connectionString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var containerNameField = typeof(AzureBlobStorageService).GetField("_containerName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            connectionStringField.GetValue(service).Should().Be("DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=test-key;EndpointSuffix=core.windows.net");
            containerNameField.GetValue(service).Should().Be("wikipedia-data");
            
            // Verify configuration was accessed
            configMock.Verify(c => c["Storage:ConnectionString"], Times.Once);
            configMock.Verify(c => c["Storage:ContainerName"], Times.Once);
        }
        
        [Fact]
        public void WikipediaDataIngestionFunction_UsesCorrectConfigurationSettings()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<WikipediaDataIngestionFunction>>();
            
            // Setup expected configuration values
            configMock.Setup(c => c["Ingestion:MaxArticles"]).Returns("25");
            
            // Set up mock services
            var wikipediaServiceMock = new Mock<IWikipediaService>();
            var textProcessingServiceMock = new Mock<ITextProcessingService>();
            var embeddingServiceMock = new Mock<IAzureOpenAIEmbeddingService>();
            var searchIndexServiceMock = new Mock<IAzureSearchIndexService>();
            var blobStorageServiceMock = new Mock<IAzureBlobStorageService>();
            
            // Act
            var function = new WikipediaDataIngestionFunction(
                wikipediaServiceMock.Object,
                textProcessingServiceMock.Object,
                embeddingServiceMock.Object,
                searchIndexServiceMock.Object,
                blobStorageServiceMock.Object,
                loggerMock.Object,
                configMock.Object
            );
            
            // Assert - Testing private fields requires reflection
            var maxArticlesField = typeof(WikipediaDataIngestionFunction).GetField("_maxArticles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxArticlesField.GetValue(function).Should().Be(25);
            
            // Verify configuration was accessed
            configMock.Verify(c => c["Ingestion:MaxArticles"], Times.Once);
        }
        
        [Fact]
        public void TextProcessingService_UsesCorrectConfigurationSettings()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<TextProcessingService>>();
            
            // Setup expected configuration values
            configMock.Setup(c => c["TextProcessing:MaxTokensPerChunk"]).Returns("512");
            configMock.Setup(c => c["TextProcessing:OverlapSize"]).Returns("100");
            
            // Act
            var service = new TextProcessingService(configMock.Object, loggerMock.Object);
            
            // Assert - Testing private fields requires reflection
            var maxTokensField = typeof(TextProcessingService).GetField("_maxTokensPerChunk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var overlapSizeField = typeof(TextProcessingService).GetField("_overlapSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxTokensField.GetValue(service).Should().Be(512);
            overlapSizeField.GetValue(service).Should().Be(100);
            
            // Verify configuration was accessed
            configMock.Verify(c => c["TextProcessing:MaxTokensPerChunk"], Times.Once);
            configMock.Verify(c => c["TextProcessing:OverlapSize"], Times.Once);
        }
        
        [Theory]
        [InlineData("", 10)] // Default fallback value
        [InlineData("invalid", 10)] // Default fallback for invalid values
        [InlineData("5", 5)]
        [InlineData("100", 100)]
        public void AllServices_HandleInvalidConfigurationValues(string configValue, int expectedValue)
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<WikipediaDataIngestionFunction>>();
            
            // Setup configuration to return test value
            configMock.Setup(c => c["Ingestion:MaxArticles"]).Returns(configValue);
            
            // Set up mock services
            var wikipediaServiceMock = new Mock<IWikipediaService>();
            var textProcessingServiceMock = new Mock<ITextProcessingService>();
            var embeddingServiceMock = new Mock<IAzureOpenAIEmbeddingService>();
            var searchIndexServiceMock = new Mock<IAzureSearchIndexService>();
            var blobStorageServiceMock = new Mock<IAzureBlobStorageService>();
            
            // Act
            var function = new WikipediaDataIngestionFunction(
                wikipediaServiceMock.Object,
                textProcessingServiceMock.Object,
                embeddingServiceMock.Object,
                searchIndexServiceMock.Object,
                blobStorageServiceMock.Object,
                loggerMock.Object,
                configMock.Object
            );
            
            // Assert - Testing private fields requires reflection
            var maxArticlesField = typeof(WikipediaDataIngestionFunction).GetField("_maxArticles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // We expect default values if invalid
            if (string.IsNullOrEmpty(configValue) || !int.TryParse(configValue, out _))
            {
                maxArticlesField.GetValue(function).Should().Be(expectedValue);
                
                // Verify warning was logged for invalid values
                if (configValue == "invalid")
                {
                    loggerMock.Verify(
                        x => x.Log(
                            LogLevel.Warning,
                            It.IsAny<EventId>(),
                            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Invalid configuration value")),
                            It.IsAny<Exception>(),
                            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                        Times.AtLeastOnce);
                }
            }
            else
            {
                maxArticlesField.GetValue(function).Should().Be(expectedValue);
            }
        }
    }
} 