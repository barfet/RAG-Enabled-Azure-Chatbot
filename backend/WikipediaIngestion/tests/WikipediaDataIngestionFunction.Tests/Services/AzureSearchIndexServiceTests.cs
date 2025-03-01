using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WikipediaDataIngestionFunction.Services;
using Xunit;
using FluentAssertions;

namespace WikipediaDataIngestionFunction.Tests.Services
{
    public class AzureSearchIndexServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<ILogger<AzureSearchIndexService>> _loggerMock;
        private readonly Mock<SearchIndexClient> _searchIndexClientMock;
        private readonly Mock<SearchClient> _searchClientMock;

        public AzureSearchIndexServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<AzureSearchIndexService>>();
            _searchIndexClientMock = new Mock<SearchIndexClient>();
            _searchClientMock = new Mock<SearchClient>();

            // Setup configuration
            _configMock.Setup(c => c["Search__Endpoint"]).Returns("https://test-search.search.windows.net");
            _configMock.Setup(c => c["Search__Key"]).Returns("test-key");
            _configMock.Setup(c => c["Search__IndexName"]).Returns("wikipedia-index");
        }

        [Fact]
        public async Task CreateIndexIfNotExistsAsync_WhenIndexExists_DoesNotCreateNewIndex()
        {
            // Arrange
            var responseMock = new Mock<Response<SearchIndex>>();
            responseMock.Setup(r => r.Value).Returns(new SearchIndex("wikipedia-index"));

            _searchIndexClientMock
                .Setup(c => c.GetIndexAsync("wikipedia-index", It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMock.Object);

            var service = new TestableAzureSearchIndexService(
                _configMock.Object,
                _loggerMock.Object,
                _searchIndexClientMock.Object,
                _searchClientMock.Object);

            // Act
            await service.CreateIndexIfNotExistsAsync();

            // Assert
            _searchIndexClientMock.Verify(
                c => c.CreateOrUpdateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateIndexIfNotExistsAsync_WhenIndexDoesNotExist_CreatesNewIndex()
        {
            // Arrange
            _searchIndexClientMock
                .Setup(c => c.GetIndexAsync("wikipedia-index", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(404, "Not Found"));

            SearchIndex capturedIndex = null;
            _searchIndexClientMock
                .Setup(c => c.CreateOrUpdateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<CancellationToken>()))
                .Callback<SearchIndex, CancellationToken>((index, _) => capturedIndex = index)
                .ReturnsAsync(new Mock<Response<SearchIndex>>().Object);

            var service = new TestableAzureSearchIndexService(
                _configMock.Object,
                _loggerMock.Object,
                _searchIndexClientMock.Object,
                _searchClientMock.Object);

            // Act
            await service.CreateIndexIfNotExistsAsync();

            // Assert
            _searchIndexClientMock.Verify(
                c => c.CreateOrUpdateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify the index schema
            capturedIndex.Should().NotBeNull();
            capturedIndex.Name.Should().Be("wikipedia-index");
            
            // Verify vector search configuration
            capturedIndex.VectorSearch.Should().NotBeNull();
            capturedIndex.VectorSearch.Algorithms.Should().HaveCount(1);
            capturedIndex.VectorSearch.Algorithms[0].Should().BeOfType<HnswAlgorithmConfiguration>();
            var hnsw = (HnswAlgorithmConfiguration)capturedIndex.VectorSearch.Algorithms[0];
            hnsw.Name.Should().Be("my-vector-config");
            hnsw.Parameters.Metric.Should().Be(VectorSearchAlgorithmMetric.Cosine);
            
            // Verify semantic search configuration
            capturedIndex.SemanticSearch.Should().NotBeNull();
            capturedIndex.SemanticSearch.Configurations.Should().HaveCount(1);
            var semanticConfig = capturedIndex.SemanticSearch.Configurations[0];
            semanticConfig.Name.Should().Be("my-semantic-config");
            semanticConfig.PrioritizedFields.TitleField.FieldName.Should().Be("title");
        }

        [Fact]
        public async Task IndexChunksAsync_WithNullOrEmptyChunks_ReturnsWithoutIndexing()
        {
            // Arrange
            var service = new TestableAzureSearchIndexService(
                _configMock.Object,
                _loggerMock.Object,
                _searchIndexClientMock.Object,
                _searchClientMock.Object);

            // Act - with null
            await service.IndexChunksAsync(null);

            // Act - with empty list
            await service.IndexChunksAsync(new List<TextChunk>());

            // Assert
            _searchClientMock.Verify(
                c => c.IndexDocumentsAsync(It.IsAny<IndexDocumentsBatch<object>>(), It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task IndexChunksAsync_WithChunks_IndexesDocumentsCorrectly()
        {
            // Arrange
            var chunks = new List<TextChunk>
            {
                new TextChunk
                {
                    Id = "chunk1",
                    ArticleId = "article1",
                    Title = "Test Article",
                    Content = "Test content for chunk 1",
                    Section = "Section 1",
                    Url = "https://en.wikipedia.org/wiki/Test_Article",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Category1", "Category2" },
                    ContentVector = new float[] { 0.1f, 0.2f, 0.3f }
                },
                new TextChunk
                {
                    Id = "chunk2",
                    ArticleId = "article1",
                    Title = "Test Article",
                    Content = "Test content for chunk 2",
                    Section = "Section 2",
                    Url = "https://en.wikipedia.org/wiki/Test_Article",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Category1", "Category3" },
                    ContentVector = new float[] { 0.4f, 0.5f, 0.6f }
                }
            };

            // Mock successful response
            var results = new List<IndexingResult>
            {
                new IndexingResult("chunk1", true, null, null, null),
                new IndexingResult("chunk2", true, null, null, null)
            };
            var indexDocumentsResult = new IndexDocumentsResult(results, null);
            
            var responseMock = new Mock<Response<IndexDocumentsResult>>();
            responseMock.Setup(r => r.Value).Returns(indexDocumentsResult);

            _searchClientMock
                .Setup(c => c.IndexDocumentsAsync(
                    It.IsAny<IndexDocumentsBatch<object>>(),
                    It.IsAny<IndexDocumentsOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMock.Object);

            var service = new TestableAzureSearchIndexService(
                _configMock.Object,
                _loggerMock.Object,
                _searchIndexClientMock.Object,
                _searchClientMock.Object);

            // Act
            await service.IndexChunksAsync(chunks);

            // Assert
            _searchClientMock.Verify(
                c => c.IndexDocumentsAsync(
                    It.IsAny<IndexDocumentsBatch<object>>(),
                    It.IsAny<IndexDocumentsOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task IndexChunksAsync_WithLargeNumberOfChunks_BatchesRequests()
        {
            // Arrange
            var largeChunkList = new List<TextChunk>();
            for (int i = 0; i < 1500; i++) // More than default batch size (1000)
            {
                largeChunkList.Add(new TextChunk
                {
                    Id = $"chunk{i}",
                    ArticleId = "article1",
                    Title = "Test Article",
                    Content = $"Test content for chunk {i}",
                    Section = $"Section {i % 10}",
                    Url = "https://en.wikipedia.org/wiki/Test_Article",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Category1" },
                    ContentVector = new float[] { 0.1f, 0.2f, 0.3f }
                });
            }

            // Mock successful response
            var successResults = new List<IndexingResult>();
            for (int i = 0; i < 1000; i++)
            {
                successResults.Add(new IndexingResult($"chunk{i}", true, null, null, null));
            }
            var successIndexDocumentsResult = new IndexDocumentsResult(successResults, null);
            
            var responseMock1 = new Mock<Response<IndexDocumentsResult>>();
            responseMock1.Setup(r => r.Value).Returns(successIndexDocumentsResult);

            var successResults2 = new List<IndexingResult>();
            for (int i = 1000; i < 1500; i++)
            {
                successResults2.Add(new IndexingResult($"chunk{i}", true, null, null, null));
            }
            var successIndexDocumentsResult2 = new IndexDocumentsResult(successResults2, null);
            
            var responseMock2 = new Mock<Response<IndexDocumentsResult>>();
            responseMock2.Setup(r => r.Value).Returns(successIndexDocumentsResult2);

            _searchClientMock
                .SetupSequence(c => c.IndexDocumentsAsync(
                    It.IsAny<IndexDocumentsBatch<object>>(),
                    It.IsAny<IndexDocumentsOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMock1.Object)
                .ReturnsAsync(responseMock2.Object);

            var service = new TestableAzureSearchIndexService(
                _configMock.Object,
                _loggerMock.Object,
                _searchIndexClientMock.Object,
                _searchClientMock.Object);

            // Act
            await service.IndexChunksAsync(largeChunkList);

            // Assert - should be called twice for the two batches
            _searchClientMock.Verify(
                c => c.IndexDocumentsAsync(
                    It.IsAny<IndexDocumentsBatch<object>>(),
                    It.IsAny<IndexDocumentsOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task IndexChunksAsync_WithFailedIndexing_LogsErrors()
        {
            // Arrange
            var chunks = new List<TextChunk>
            {
                new TextChunk
                {
                    Id = "chunk1",
                    ArticleId = "article1",
                    Title = "Test Article",
                    Content = "Test content for chunk 1",
                    Section = "Section 1",
                    Url = "https://en.wikipedia.org/wiki/Test_Article",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Category1" },
                    ContentVector = new float[] { 0.1f, 0.2f, 0.3f }
                }
            };

            // Mock response with an error
            var results = new List<IndexingResult>
            {
                new IndexingResult("chunk1", false, "Error indexing document", null, null)
            };
            var indexDocumentsResult = new IndexDocumentsResult(results, null);
            
            var responseMock = new Mock<Response<IndexDocumentsResult>>();
            responseMock.Setup(r => r.Value).Returns(indexDocumentsResult);

            _searchClientMock
                .Setup(c => c.IndexDocumentsAsync(
                    It.IsAny<IndexDocumentsBatch<object>>(),
                    It.IsAny<IndexDocumentsOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(responseMock.Object);

            var service = new TestableAzureSearchIndexService(
                _configMock.Object,
                _loggerMock.Object,
                _searchIndexClientMock.Object,
                _searchClientMock.Object);

            // Act
            await service.IndexChunksAsync(chunks);

            // Assert - should log an error for the failed document
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to index document")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        // Testable version of the service to allow dependency injection
        private class TestableAzureSearchIndexService : AzureSearchIndexService
        {
            public TestableAzureSearchIndexService(
                IConfiguration configuration,
                ILogger<AzureSearchIndexService> logger,
                SearchIndexClient searchIndexClient,
                SearchClient searchClient)
                : base(configuration, logger)
            {
                // Use reflection to set the private fields with our mocks
                var searchIndexClientField = typeof(AzureSearchIndexService)
                    .GetField("_searchIndexClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                searchIndexClientField.SetValue(this, searchIndexClient);

                var searchClientField = typeof(AzureSearchIndexService)
                    .GetField("_searchClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                searchClientField.SetValue(this, searchClient);

                var indexNameField = typeof(AzureSearchIndexService)
                    .GetField("_indexName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                indexNameField.SetValue(this, "wikipedia-index");
            }
        }
    }
} 