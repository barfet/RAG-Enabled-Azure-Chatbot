using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WikipediaDataIngestionFunction.Services;
using Xunit;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using System.Net;
using System.IO;
using WikipediaDataIngestionFunction.Functions;

namespace WikipediaDataIngestionFunction.Tests
{
    public class WikipediaDataIngestionFunctionTests
    {
        private readonly Mock<IWikipediaService> _wikipediaServiceMock;
        private readonly Mock<ITextProcessingService> _textProcessingServiceMock;
        private readonly Mock<IEmbeddingService> _embeddingServiceMock;
        private readonly Mock<ISearchIndexService> _searchIndexServiceMock;
        private readonly Mock<IStorageService> _storageServiceMock;
        private readonly Mock<ILogger<Functions.WikipediaDataIngestionFunction>> _loggerMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Functions.WikipediaDataIngestionFunction _function;

        public WikipediaDataIngestionFunctionTests()
        {
            _wikipediaServiceMock = new Mock<IWikipediaService>();
            _textProcessingServiceMock = new Mock<ITextProcessingService>();
            _embeddingServiceMock = new Mock<IEmbeddingService>();
            _searchIndexServiceMock = new Mock<ISearchIndexService>();
            _storageServiceMock = new Mock<IStorageService>();
            _loggerMock = new Mock<ILogger<Functions.WikipediaDataIngestionFunction>>();
            _configMock = new Mock<IConfiguration>();

            // Setup configuration
            _configMock.Setup(c => c["Ingestion:MaxArticles"]).Returns("10");
            _configMock.Setup(c => c["Ingestion:ChunkSize"]).Returns("1000");
            _configMock.Setup(c => c["Ingestion:ChunkOverlap"]).Returns("100");
            
            _function = new Functions.WikipediaDataIngestionFunction(
                _wikipediaServiceMock.Object,
                _textProcessingServiceMock.Object,
                _embeddingServiceMock.Object,
                _searchIndexServiceMock.Object,
                _storageServiceMock.Object,
                _configMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task RunManual_ProcessesArticlesSuccessfully()
        {
            // Arrange - mock an HttpRequestData
            var mockRequest = CreateMockRequest();

            var articles = new List<WikipediaArticle>
            {
                new WikipediaArticle
                {
                    Id = "article1",
                    Title = "Test Article 1",
                    Content = "This is test content for article 1.",
                    Url = "https://en.wikipedia.org/wiki/Test_Article_1",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Test", "Example" }
                },
                new WikipediaArticle
                {
                    Id = "article2",
                    Title = "Test Article 2",
                    Content = "This is test content for article 2.",
                    Url = "https://en.wikipedia.org/wiki/Test_Article_2",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Test", "Example" }
                }
            };

            _wikipediaServiceMock
                .Setup(s => s.GetArticlesAsync(It.IsAny<int>()))
                .ReturnsAsync(articles);

            // Setup text processing to return chunks
            var chunks = new List<TextChunk>
            {
                new TextChunk
                {
                    Id = "article1-chunk1",
                    Title = "Test Article 1",
                    Content = "This is test content for article 1.",
                    Section = "",
                    Url = "https://en.wikipedia.org/wiki/Test_Article_1",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Test", "Example" },
                    ContentVector = new float[] { 0.1f, 0.2f, 0.3f }
                },
                new TextChunk
                {
                    Id = "article2-chunk1",
                    Title = "Test Article 2",
                    Content = "This is test content for article 2.",
                    Section = "",
                    Url = "https://en.wikipedia.org/wiki/Test_Article_2",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Test", "Example" },
                    ContentVector = new float[] { 0.4f, 0.5f, 0.6f }
                }
            };

            _textProcessingServiceMock
                .Setup(s => s.ProcessArticleIntoChunksAsync(It.IsAny<WikipediaArticle>()))
                .ReturnsAsync((WikipediaArticle article) => new List<TextChunk>
                {
                    new TextChunk
                    {
                        Id = $"{article.Id}-chunk1",
                        Title = article.Title,
                        Content = article.Content,
                        Section = "",
                        Url = article.Url,
                        LastUpdated = DateTime.UtcNow,
                        Categories = article.Categories,
                        ContentVector = new float[] { 0.1f, 0.2f, 0.3f }
                    }
                });

            // Setup embedding service to return embeddings
            _embeddingServiceMock
                .Setup(s => s.GenerateEmbeddingsAsync(It.IsAny<List<TextChunk>>()))
                .ReturnsAsync((List<TextChunk> chunks) => chunks);

            // Setup search index creation
            _searchIndexServiceMock
                .Setup(s => s.CreateIndexIfNotExistsAsync())
                .Returns(Task.CompletedTask);

            // Setup search index to index chunks
            _searchIndexServiceMock
                .Setup(s => s.IndexChunksAsync(It.IsAny<List<TextChunk>>()))
                .Returns(Task.CompletedTask);

            // Setup storage to save raw articles
            _storageServiceMock
                .Setup(s => s.SaveRawArticleAsync(It.IsAny<WikipediaArticle>()))
                .Returns(Task.CompletedTask);

            // Act
            var response = await _function.RunManual(mockRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            // Verify services were called
            _wikipediaServiceMock.Verify(s => s.GetArticlesAsync(10), Times.Once);
            
            _textProcessingServiceMock.Verify(
                s => s.ProcessArticleIntoChunksAsync(It.Is<WikipediaArticle>(a => a.Id == "article1")),
                Times.Once);
            
            _textProcessingServiceMock.Verify(
                s => s.ProcessArticleIntoChunksAsync(It.Is<WikipediaArticle>(a => a.Id == "article2")),
                Times.Once);
            
            _embeddingServiceMock.Verify(
                s => s.GenerateEmbeddingsAsync(It.IsAny<List<TextChunk>>()),
                Times.Once);
            
            _searchIndexServiceMock.Verify(
                s => s.CreateIndexIfNotExistsAsync(),
                Times.Once);
            
            _searchIndexServiceMock.Verify(
                s => s.IndexChunksAsync(It.IsAny<List<TextChunk>>()),
                Times.Once);
            
            _storageServiceMock.Verify(
                s => s.SaveRawArticleAsync(It.Is<WikipediaArticle>(a => a.Id == "article1")),
                Times.Once);
            
            _storageServiceMock.Verify(
                s => s.SaveRawArticleAsync(It.Is<WikipediaArticle>(a => a.Id == "article2")),
                Times.Once);
        }

        private HttpRequestData CreateMockRequest()
        {
            var mockRequest = new Mock<HttpRequestData>(MockBehavior.Strict, Mock.Of<FunctionContext>());
            var mockResponse = new Mock<HttpResponseData>(MockBehavior.Strict, Mock.Of<FunctionContext>());
            
            mockResponse.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
            mockResponse.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
            mockResponse.Setup(r => r.Body).Returns(new MemoryStream());
            
            mockRequest.Setup(r => r.CreateResponse()).Returns(mockResponse.Object);
            
            return mockRequest.Object;
        }
    }
} 