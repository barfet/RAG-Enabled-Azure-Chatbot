using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WikipediaDataIngestionFunction.Services;
using Xunit;
using FluentAssertions;

namespace WikipediaDataIngestionFunction.Tests.Services
{
    public class AzureOpenAIEmbeddingServiceTests
    {
        private readonly Mock<AzureOpenAIClient> _mockAzureOpenAIClient;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<ILogger<AzureOpenAIEmbeddingService>> _mockLogger;
        private readonly TestableAzureOpenAIEmbeddingService _service;
        private readonly string _deploymentName = "test-embedding-deployment";

        public AzureOpenAIEmbeddingServiceTests()
        {
            _mockAzureOpenAIClient = new Mock<AzureOpenAIClient>();
            _mockConfig = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<AzureOpenAIEmbeddingService>>();
            
            // Setup configuration
            _mockConfig.Setup(c => c["OpenAI__Endpoint"]).Returns("https://test.openai.azure.com");
            _mockConfig.Setup(c => c["OpenAI__Key"]).Returns("test-key");
            _mockConfig.Setup(c => c["OpenAI__EmbeddingsModelDeployment"]).Returns(_deploymentName);
            
            _service = new TestableAzureOpenAIEmbeddingService(
                _mockConfig.Object,
                _mockLogger.Object,
                _mockAzureOpenAIClient.Object);
        }

        [Fact]
        public async Task GenerateEmbeddingAsync_ReturnsEmbedding_WhenSuccessful()
        {
            // Arrange
            var text = "Test text";
            var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
            
            var embeddingItem = new EmbeddingItem(
                new ReadOnlyMemory<float>(expectedEmbedding),
                0,
                null);
            
            var embeddingsResult = new Embeddings(
                new[] { embeddingItem },
                "test-model",
                null,
                null);
            
            var response = Response.FromValue(embeddingsResult, Mock.Of<Response>());
            
            _mockAzureOpenAIClient
                .Setup(client => client.GetEmbeddingsAsync(
                    It.Is<EmbeddingsOptions>(options => 
                        options.DeploymentName == _deploymentName && 
                        options.Input.Contains(text)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            // Act
            var result = await _service.GenerateEmbeddingAsync(text);

            // Assert
            result.Should().BeEquivalentTo(expectedEmbedding);
            _mockAzureOpenAIClient.Verify(client => client.GetEmbeddingsAsync(
                It.Is<EmbeddingsOptions>(options => 
                    options.DeploymentName == _deploymentName && 
                    options.Input.Contains(text)),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateEmbeddingsAsync_ProcessesAllChunks()
        {
            // Arrange
            var chunks = new List<TextChunk>
            {
                new TextChunk { Id = "1", Content = "Text 1" },
                new TextChunk { Id = "2", Content = "Text 2" }
            };
            
            // Create different embeddings for different texts
            var embedding1 = new float[] { 0.1f, 0.2f, 0.3f };
            var embedding2 = new float[] { 0.4f, 0.5f, 0.6f };
            
            var embeddingData1 = new EmbeddingItem(
                new ReadOnlyMemory<float>(embedding1),
                0,
                null);
            
            var embeddingData2 = new EmbeddingItem(
                new ReadOnlyMemory<float>(embedding2),
                0,
                null);
            
            // Setup OpenAI client to return different embeddings for different texts
            _mockAzureOpenAIClient
                .Setup(c => c.GetEmbeddingsAsync(
                    It.Is<EmbeddingsOptions>(options => 
                        options.Input.Contains("Text 1")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMockResponse(new[] { embeddingData1 }));
            
            _mockAzureOpenAIClient
                .Setup(c => c.GetEmbeddingsAsync(
                    It.Is<EmbeddingsOptions>(options => 
                        options.Input.Contains("Text 2")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMockResponse(new[] { embeddingData2 }));

            // Act
            var result = await _service.GenerateEmbeddingsAsync(chunks);

            // Assert
            result.Should().HaveCount(2);
            result[0].ContentVector.Should().BeEquivalentTo(embedding1);
            result[1].ContentVector.Should().BeEquivalentTo(embedding2);
        }

        [Fact]
        public async Task GenerateEmbeddingAsync_RetriesOnRateLimitExceeded()
        {
            // Arrange
            var text = "Test text";
            
            // First call throws TooManyRequests error, second succeeds
            _mockAzureOpenAIClient
                .SetupSequence(c => c.GetEmbeddingsAsync(
                    It.Is<EmbeddingsOptions>(options => 
                        options.DeploymentName == _deploymentName && 
                        options.Input.Contains(text)),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(429, "Too many requests"))
                .ReturnsAsync(CreateMockResponse(new[] { new EmbeddingItem(
                    new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f }),
                    0,
                    null) }));

            // Act
            var result = await _service.GenerateEmbeddingAsync(text);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new float[] { 0.1f, 0.2f, 0.3f });
            
            // Verify GetEmbeddingsAsync was called exactly twice
            _mockAzureOpenAIClient.Verify(
                c => c.GetEmbeddingsAsync(
                    It.Is<EmbeddingsOptions>(options => 
                        options.DeploymentName == _deploymentName && 
                        options.Input.Contains(text)),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GenerateEmbeddingAsync_RetriesOnGenericError()
        {
            // Arrange
            var text = "Test text";
            
            // First call throws generic error, second succeeds
            _mockAzureOpenAIClient
                .SetupSequence(c => c.GetEmbeddingsAsync(
                    It.Is<EmbeddingsOptions>(options => 
                        options.DeploymentName == _deploymentName && 
                        options.Input.Contains(text)),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Generic error"))
                .ReturnsAsync(CreateMockResponse(new[] { new EmbeddingItem(
                    new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f }),
                    0,
                    null) }));

            // Act
            var result = await _service.GenerateEmbeddingAsync(text);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new float[] { 0.1f, 0.2f, 0.3f });
        }

        [Fact]
        public async Task GenerateEmbeddingAsync_ThrowsAfterMaxRetries()
        {
            // Arrange
            var text = "Test text";
            
            // All calls throw TooManyRequests error
            _mockAzureOpenAIClient
                .Setup(c => c.GetEmbeddingsAsync(
                    It.Is<EmbeddingsOptions>(options => 
                        options.DeploymentName == _deploymentName && 
                        options.Input.Contains(text)),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(429, "Too many requests"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => 
                _service.GenerateEmbeddingAsync(text));
            
            // Verify GetEmbeddingsAsync was called exactly maxRetries times
            _mockAzureOpenAIClient.Verify(
                c => c.GetEmbeddingsAsync(
                    It.Is<EmbeddingsOptions>(options => 
                        options.DeploymentName == _deploymentName && 
                        options.Input.Contains(text)),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(5)); // MaxRetries = 5
        }

        private Response<Embeddings> CreateMockResponse(IEnumerable<EmbeddingItem> embeddingItems)
        {
            var result = new Embeddings(
                embeddingItems,
                "test-model",
                null,
                null);
            
            return Response.FromValue(result, Mock.Of<Response>());
        }

        private class TestableAzureOpenAIEmbeddingService : AzureOpenAIEmbeddingService
        {
            private readonly AzureOpenAIClient _testClient;

            public TestableAzureOpenAIEmbeddingService(
                IConfiguration configuration,
                ILogger<AzureOpenAIEmbeddingService> logger,
                AzureOpenAIClient testClient) 
                : base(configuration, logger)
            {
                _testClient = testClient;
            }

            protected override AzureOpenAIClient CreateClient(Uri endpoint, AzureKeyCredential credential)
            {
                return _testClient;
            }
        }
    }
} 