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
    public class AzureOpenAIEmbeddingGeneratorTests
    {
        [Fact]
        public async Task GenerateEmbeddingsAsync_ShouldReturnEmptyDictionary_WhenNoChunksProvided()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            
            var embeddingGenerator = new AzureOpenAIEmbeddingGenerator(
                httpClient, 
                "https://test-endpoint", 
                "test-deployment", 
                "test-api-key",
                "test-api-version");

            // Act
            var embeddings = await embeddingGenerator.GenerateEmbeddingsAsync(new List<ArticleChunk>());

            // Assert
            Assert.Empty(embeddings);
        }

        [Fact]
        public async Task GenerateEmbeddingsAsync_ShouldReturnEmbeddings_WhenChunksProvided()
        {
            // Arrange
            var chunks = new List<ArticleChunk>
            {
                new ArticleChunk
                {
                    Id = "chunk1",
                    ArticleId = "article1",
                    Content = "This is test chunk 1",
                    SectionTitle = "Introduction"
                },
                new ArticleChunk
                {
                    Id = "chunk2",
                    ArticleId = "article1",
                    Content = "This is test chunk 2",
                    SectionTitle = "History"
                }
            };

            // Set up mock handler to return different responses for each request
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            
            // First request (for chunk1)
            mockHttpMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonConvert.SerializeObject(new
                    {
                        data = new[]
                        {
                            new
                            {
                                embedding = new[] { 0.1f, 0.2f, 0.3f }
                            }
                        }
                    }))
                })
                // Second request (for chunk2)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonConvert.SerializeObject(new
                    {
                        data = new[]
                        {
                            new
                            {
                                embedding = new[] { 0.4f, 0.5f, 0.6f }
                            }
                        }
                    }))
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var embeddingGenerator = new AzureOpenAIEmbeddingGenerator(
                httpClient,
                "https://test-endpoint",
                "test-deployment",
                "test-api-key",
                "test-api-version");

            // Act
            var embeddings = await embeddingGenerator.GenerateEmbeddingsAsync(chunks);

            // Assert
            Assert.Equal(2, embeddings.Count);
            Assert.True(embeddings.ContainsKey("chunk1"));
            Assert.True(embeddings.ContainsKey("chunk2"));
            Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, embeddings["chunk1"]);
            Assert.Equal(new[] { 0.4f, 0.5f, 0.6f }, embeddings["chunk2"]);
        }

        [Fact]
        public async Task GenerateEmbeddingsAsync_ShouldThrowException_WhenApiReturnsError()
        {
            // Arrange
            var chunks = new List<ArticleChunk>
            {
                new ArticleChunk
                {
                    Id = "chunk1",
                    ArticleId = "article1",
                    Content = "This is test chunk 1",
                    SectionTitle = "Introduction"
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
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("Invalid request")
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var embeddingGenerator = new AzureOpenAIEmbeddingGenerator(
                httpClient,
                "https://test-endpoint",
                "test-deployment",
                "test-api-key",
                "test-api-version");

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                embeddingGenerator.GenerateEmbeddingsAsync(chunks));
        }

        [Fact]
        public async Task GenerateEmbeddingsAsync_ShouldSendCorrectRequest()
        {
            // Arrange
            var chunks = new List<ArticleChunk>
            {
                new ArticleChunk
                {
                    Id = "chunk1",
                    ArticleId = "article1",
                    Content = "This is test chunk 1",
                    SectionTitle = "Introduction"
                }
            };

            HttpRequestMessage? capturedRequest = null;
            string? capturedContent = null;

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (request, _) => 
                {
                    capturedRequest = request;
                    if (request.Content != null)
                    {
                        capturedContent = await request.Content.ReadAsStringAsync();
                    }
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonConvert.SerializeObject(new
                    {
                        data = new[]
                        {
                            new
                            {
                                embedding = new[] { 0.1f, 0.2f, 0.3f }
                            }
                        }
                    }))
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var embeddingGenerator = new AzureOpenAIEmbeddingGenerator(
                httpClient,
                "https://test-endpoint",
                "test-deployment",
                "test-api-key",
                "test-api-version");

            // Act
            await embeddingGenerator.GenerateEmbeddingsAsync(chunks);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
            Assert.NotNull(capturedRequest.RequestUri);
            Assert.Equal("test-api-key", capturedRequest.Headers.GetValues("api-key").First());
            Assert.Contains("deployments/test-deployment/embeddings", capturedRequest.RequestUri!.ToString());
            Assert.Contains("api-version=test-api-version", capturedRequest.RequestUri!.ToString());
            
            Assert.NotNull(capturedContent);
            var requestBody = JsonConvert.DeserializeObject<dynamic>(capturedContent!);
            Assert.NotNull(requestBody);
            Assert.Equal("text-embedding-ada-002", (string?)requestBody!.model);
            Assert.Single((Newtonsoft.Json.Linq.JArray)requestBody!.input);
            Assert.Equal("This is test chunk 1", (string?)requestBody!.input[0]);
        }
    }
} 