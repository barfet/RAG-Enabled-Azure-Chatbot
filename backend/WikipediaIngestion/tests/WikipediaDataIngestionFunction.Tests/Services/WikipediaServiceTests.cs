using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using WikipediaDataIngestionFunction.Services;
using Xunit;
using FluentAssertions;

namespace WikipediaDataIngestionFunction.Tests.Services
{
    public class WikipediaServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<ILogger<WikipediaService>> _loggerMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public WikipediaServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<WikipediaService>>();
            _configMock = new Mock<IConfiguration>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

            // Set up configuration
            _configMock.Setup(c => c["Wikipedia__MaxArticlesToProcess"]).Returns("10");
        }

        [Fact]
        public async Task GetArticlesAsync_ReturnsArticlesWithMetadata()
        {
            // Arrange
            // Mock HTTP response with test Wikipedia data
            var testResponse = CreateTestHuggingFaceResponse();
            var httpClient = SetupMockHttpClient(testResponse);
            _httpClientFactoryMock.Setup(f => f.CreateClient("WikipediaClient")).Returns(httpClient);

            var wikipediaService = new WikipediaService(
                _httpClientFactoryMock.Object,
                _configMock.Object,
                _loggerMock.Object);

            // Act
            var articles = await wikipediaService.GetArticlesAsync(2);

            // Assert
            articles.Should().HaveCount(2);
            
            // Verify the first article
            articles[0].Title.Should().Be("Test Article 1");
            articles[0].Content.Should().Be("This is the content of test article 1.");
            articles[0].Url.Should().Be("https://en.wikipedia.org/wiki/Test_Article_1");
            articles[0].Categories.Should().Contain("Category1");
            articles[0].Categories.Should().Contain("Category2");
            
            // Verify the second article
            articles[1].Title.Should().Be("Test Article 2");
            articles[1].Content.Should().Be("This is the content of test article 2.");
            articles[1].Url.Should().Be("https://en.wikipedia.org/wiki/Test_Article_2");
            articles[1].Categories.Should().Contain("Category3");
        }

        [Fact]
        public async Task GetArticlesAsync_WithApiError_ReturnsEmptyList()
        {
            // Arrange
            // Set up HTTP client to return an error
            var httpClient = SetupMockHttpClientWithError(HttpStatusCode.InternalServerError);
            _httpClientFactoryMock.Setup(f => f.CreateClient("WikipediaClient")).Returns(httpClient);

            var wikipediaService = new WikipediaService(
                _httpClientFactoryMock.Object,
                _configMock.Object,
                _loggerMock.Object);

            // Act
            var articles = await wikipediaService.GetArticlesAsync(5);

            // Assert
            articles.Should().BeEmpty();
        }

        [Fact]
        public async Task GetArticlesAsync_WithEmptyResponse_ReturnsEmptyList()
        {
            // Arrange
            // Mock HTTP response with empty data
            var emptyResponse = new
            {
                rows = new object[] { }
            };
            var httpClient = SetupMockHttpClient(emptyResponse);
            _httpClientFactoryMock.Setup(f => f.CreateClient("WikipediaClient")).Returns(httpClient);

            var wikipediaService = new WikipediaService(
                _httpClientFactoryMock.Object,
                _configMock.Object,
                _loggerMock.Object);

            // Act
            var articles = await wikipediaService.GetArticlesAsync(5);

            // Assert
            articles.Should().BeEmpty();
        }

        [Fact]
        public async Task GetArticlesAsync_WithNullFields_HandlesGracefully()
        {
            // Arrange
            // Mock HTTP response with null fields
            var responseWithNulls = new
            {
                rows = new[]
                {
                    new
                    {
                        row = new
                        {
                            title = (string)null,
                            text = (string)null,
                            categories = (string)null
                        }
                    }
                }
            };
            
            var httpClient = SetupMockHttpClient(responseWithNulls);
            _httpClientFactoryMock.Setup(f => f.CreateClient("WikipediaClient")).Returns(httpClient);

            var wikipediaService = new WikipediaService(
                _httpClientFactoryMock.Object,
                _configMock.Object,
                _loggerMock.Object);

            // Act
            var articles = await wikipediaService.GetArticlesAsync(1);

            // Assert
            articles.Should().HaveCount(1);
            articles[0].Title.Should().Be("Unknown Title");
            articles[0].Content.Should().BeEmpty();
            articles[0].Categories.Should().BeEmpty();
        }

        [Fact]
        public async Task GetArticlesAsync_LimitsToMaxArticles()
        {
            // Arrange
            // Mock HTTP response with more articles than requested
            var largeResponse = CreateLargeHuggingFaceResponse(20);
            var httpClient = SetupMockHttpClient(largeResponse);
            _httpClientFactoryMock.Setup(f => f.CreateClient("WikipediaClient")).Returns(httpClient);

            var wikipediaService = new WikipediaService(
                _httpClientFactoryMock.Object,
                _configMock.Object,
                _loggerMock.Object);

            // Act - request only 5 articles
            var articles = await wikipediaService.GetArticlesAsync(5);

            // Assert
            articles.Should().HaveCount(5);
        }

        private HttpClient SetupMockHttpClient(object responseContent)
        {
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responseContent))
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            return new HttpClient(_httpMessageHandlerMock.Object);
        }

        private HttpClient SetupMockHttpClientWithError(HttpStatusCode statusCode)
        {
            var response = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent("Error")
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            return new HttpClient(_httpMessageHandlerMock.Object);
        }

        private object CreateTestHuggingFaceResponse()
        {
            return new
            {
                rows = new[]
                {
                    new
                    {
                        row = new
                        {
                            title = "Test Article 1",
                            text = "This is the content of test article 1.",
                            categories = "Category1|Category2"
                        }
                    },
                    new
                    {
                        row = new
                        {
                            title = "Test Article 2",
                            text = "This is the content of test article 2.",
                            categories = "Category3"
                        }
                    }
                }
            };
        }

        private object CreateLargeHuggingFaceResponse(int count)
        {
            var rows = new List<object>();
            
            for (int i = 0; i < count; i++)
            {
                rows.Add(new
                {
                    row = new
                    {
                        title = $"Article {i}",
                        text = $"Content of article {i}",
                        categories = $"Category{i}"
                    }
                });
            }

            return new
            {
                rows = rows.ToArray()
            };
        }
    }
} 