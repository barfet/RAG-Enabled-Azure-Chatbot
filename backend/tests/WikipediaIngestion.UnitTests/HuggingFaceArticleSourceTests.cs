using System.Net;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Models;
using WikipediaIngestion.Infrastructure.Services;
using Xunit;

namespace WikipediaIngestion.UnitTests
{
    public class HuggingFaceArticleSourceTests
    {
        [Fact]
        public async Task GetArticlesAsync_ShouldReturnEmptyCollection_WhenApiReturnsEmptyResponse()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("[]")
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var articleSource = new HuggingFaceArticleSource(httpClient, "test-api-key");

            // Act
            var articles = await articleSource.GetArticlesAsync(10, 0);

            // Assert
            Assert.Empty(articles);
        }

        [Fact]
        public async Task GetArticlesAsync_ShouldReturnArticles_WhenApiReturnsValidResponse()
        {
            // Arrange
            var mockResponse = new[] 
            {
                new 
                {
                    id = "test-id-1",
                    title = "Test Article 1",
                    text = "This is test article 1 content.",
                    url = "https://wikipedia.org/wiki/Test_Article_1",
                    timestamp = "2023-01-01T00:00:00Z",
                    categories = new[] { "Category1", "Category2" }
                },
                new 
                {
                    id = "test-id-2",
                    title = "Test Article 2",
                    text = "This is test article 2 content.",
                    url = "https://wikipedia.org/wiki/Test_Article_2",
                    timestamp = "2023-01-02T00:00:00Z",
                    categories = new[] { "Category2", "Category3" }
                }
            };

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonConvert.SerializeObject(mockResponse))
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var articleSource = new HuggingFaceArticleSource(httpClient, "test-api-key");

            // Act
            var articles = await articleSource.GetArticlesAsync(10, 0);
            var articlesList = articles.ToList();

            // Assert
            Assert.Equal(2, articlesList.Count);
            
            Assert.Equal("test-id-1", articlesList[0].Id);
            Assert.Equal("Test Article 1", articlesList[0].Title);
            Assert.Equal("This is test article 1 content.", articlesList[0].Content);
            Assert.Equal(new Uri("https://wikipedia.org/wiki/Test_Article_1"), articlesList[0].Url);
            Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), articlesList[0].LastUpdated);
            Assert.Equal(new[] { "Category1", "Category2" }, articlesList[0].Categories);
            
            Assert.Equal("test-id-2", articlesList[1].Id);
            Assert.Equal("Test Article 2", articlesList[1].Title);
            Assert.Equal("This is test article 2 content.", articlesList[1].Content);
            Assert.Equal(new Uri("https://wikipedia.org/wiki/Test_Article_2"), articlesList[1].Url);
            Assert.Equal(new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc), articlesList[1].LastUpdated);
            Assert.Equal(new[] { "Category2", "Category3" }, articlesList[1].Categories);
        }
        
        [Fact]
        public async Task GetArticlesAsync_ShouldThrowException_WhenApiReturnsError()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("Internal Server Error")
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var articleSource = new HuggingFaceArticleSource(httpClient, "test-api-key");

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => articleSource.GetArticlesAsync(10, 0));
        }
        
        [Fact]
        public async Task GetArticlesAsync_ShouldSendCorrectRequest_WithLimitAndOffsetParameters()
        {
            // Arrange
            var limit = 15;
            var offset = 30;
            HttpRequestMessage? capturedRequest = null;

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("[]")
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var articleSource = new HuggingFaceArticleSource(httpClient, "test-api-key");

            // Act
            await articleSource.GetArticlesAsync(limit, offset);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
            Assert.Contains($"limit={limit}", capturedRequest.RequestUri!.Query);
            Assert.Contains($"offset={offset}", capturedRequest.RequestUri.Query);
            Assert.Contains("Authorization", capturedRequest.Headers.Select(h => h.Key));
            Assert.Equal("Bearer test-api-key", capturedRequest.Headers.GetValues("Authorization").First());
        }
    }
} 