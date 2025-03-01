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
    public class AzureSearchIndexerTests
    {
        [Fact]
        public async Task CreateIndexIfNotExistsAsync_ShouldCreateIndex_WhenIndexDoesNotExist()
        {
            // Arrange
            var indexName = "test-index";
            
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            // First call to check if index exists
            mockHttpMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                // First response - index doesn't exist
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("")
                })
                // Second response - index created successfully
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(JsonConvert.SerializeObject(new { name = indexName }))
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            httpClient.BaseAddress = new Uri("https://test-search-service.search.windows.net");
            
            var searchIndexer = new AzureSearchIndexer(
                httpClient, 
                "test-api-key");

            // Act
            await searchIndexer.CreateIndexIfNotExistsAsync(indexName);

            // Assert
            mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(2),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }
        
        [Fact]
        public async Task CreateIndexIfNotExistsAsync_ShouldNotCreateIndex_WhenIndexExists()
        {
            // Arrange
            var indexName = "test-index";
            
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonConvert.SerializeObject(new { name = indexName }))
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            httpClient.BaseAddress = new Uri("https://test-search-service.search.windows.net");
            
            var searchIndexer = new AzureSearchIndexer(
                httpClient, 
                "test-api-key");

            // Act
            await searchIndexer.CreateIndexIfNotExistsAsync(indexName);

            // Assert
            mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }
        
        [Fact]
        public async Task CreateIndexIfNotExistsAsync_ShouldCreateIndexWithCorrectSchema()
        {
            // Arrange
            var indexName = "test-index";
            var capturedRequests = new List<HttpRequestMessage>();
            
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => 
                {
                    capturedRequests.Add(request);
                })
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
                {
                    if (request.Method == HttpMethod.Get)
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.NotFound,
                            Content = new StringContent("")
                        };
                    }
                    else
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.Created,
                            Content = new StringContent(JsonConvert.SerializeObject(new { name = indexName }))
                        };
                    }
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            httpClient.BaseAddress = new Uri("https://test-search-service.search.windows.net");
            
            var searchIndexer = new AzureSearchIndexer(
                httpClient, 
                "test-api-key");

            // Act
            await searchIndexer.CreateIndexIfNotExistsAsync(indexName);

            // Assert
            Assert.Equal(2, capturedRequests.Count);
            
            // Verify the second request was a PUT with the correct content
            var putRequest = capturedRequests.Last();
            Assert.Equal(HttpMethod.Put, putRequest.Method);
            
            Assert.NotNull(putRequest.Content);
            var content = await putRequest.Content!.ReadAsStringAsync();
            var requestBody = JsonConvert.DeserializeObject<dynamic>(content);
            
            // Verify index name
            Assert.NotNull(requestBody);
            Assert.Equal(indexName, (string?)requestBody!.name);
            
            // Verify fields exist
            var fields = (Newtonsoft.Json.Linq.JArray?)requestBody!.fields;
            Assert.NotNull(fields);
            Assert.Contains(fields!, field => (string?)field["name"] == "id");
            Assert.Contains(fields!, field => (string?)field["name"] == "articleId");
            Assert.Contains(fields!, field => (string?)field["name"] == "title");
            Assert.Contains(fields!, field => (string?)field["name"] == "content");
            Assert.Contains(fields!, field => (string?)field["name"] == "section");
            Assert.Contains(fields!, field => (string?)field["name"] == "url");
            Assert.Contains(fields!, field => (string?)field["name"] == "contentVector");
            
            // Verify vector search configuration exists
            Assert.NotNull(requestBody!.vectorSearch);
            
            // Verify semantic configuration exists
            Assert.NotNull(requestBody!.semantic);
        }
        
        [Fact]
        public async Task UploadDocumentsAsync_ShouldUploadChunksWithEmbeddings()
        {
            // Arrange
            var indexName = "test-index";
            var chunks = new List<ArticleChunk>
            {
                new ArticleChunk
                {
                    Id = "chunk1",
                    ArticleId = "article1",
                    ArticleTitle = "Test Article 1",
                    Content = "This is test chunk 1",
                    SectionTitle = "Introduction",
                    ArticleUrl = "https://wikipedia.org/Test_Article_1"
                },
                new ArticleChunk
                {
                    Id = "chunk2",
                    ArticleId = "article1",
                    ArticleTitle = "Test Article 1",
                    Content = "This is test chunk 2",
                    SectionTitle = "History",
                    ArticleUrl = "https://wikipedia.org/Test_Article_1"
                }
            };
            
            var embeddings = new Dictionary<string, float[]>
            {
                ["chunk1"] = new[] { 0.1f, 0.2f, 0.3f },
                ["chunk2"] = new[] { 0.4f, 0.5f, 0.6f }
            };
            
            var capturedRequest = new HttpRequestMessage();
            
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => 
                {
                    capturedRequest = request;
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonConvert.SerializeObject(new { value = new[] { new { key = "success" } } }))
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            httpClient.BaseAddress = new Uri("https://test-search-service.search.windows.net");
            
            var searchIndexer = new AzureSearchIndexer(
                httpClient, 
                "test-api-key");

            // Act
            await searchIndexer.UploadDocumentsAsync(indexName, chunks, embeddings);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest.Method);
            Assert.NotNull(capturedRequest.RequestUri);
            Assert.Contains($"indexes/{indexName}/docs/index", capturedRequest.RequestUri!.ToString());
            Assert.Equal("test-api-key", capturedRequest.Headers.GetValues("api-key").First());
            
            Assert.NotNull(capturedRequest.Content);
            var content = await capturedRequest.Content!.ReadAsStringAsync();
            var requestBody = JsonConvert.DeserializeObject<dynamic>(content);
            Assert.NotNull(requestBody);
            var documents = (Newtonsoft.Json.Linq.JArray?)requestBody!.value;
            Assert.NotNull(documents);
            
            // Check number of documents
            Assert.Equal(2, documents!.Count);
            
            // Check first document
            var doc1 = documents[0];
            Assert.NotNull(doc1);
            Assert.Equal("chunk1", (string?)doc1["id"]);
            Assert.Equal("article1", (string?)doc1["articleId"]);
            Assert.Equal("Test Article 1", (string?)doc1["title"]);
            Assert.Equal("This is test chunk 1", (string?)doc1["content"]);
            Assert.Equal("Introduction", (string?)doc1["section"]);
            Assert.Equal("https://wikipedia.org/Test_Article_1", (string?)doc1["url"]);
            
            // Check embeddings are included
            var vectors1 = (Newtonsoft.Json.Linq.JArray?)doc1["contentVector"];
            Assert.NotNull(vectors1);
            Assert.Equal(3, vectors1!.Count);
            Assert.Equal(0.1f, (float)vectors1[0]);
            Assert.Equal(0.2f, (float)vectors1[1]);
            Assert.Equal(0.3f, (float)vectors1[2]);
            
            // Check second document
            var doc2 = documents[1];
            Assert.NotNull(doc2);
            Assert.Equal("chunk2", (string?)doc2["id"]);
            var vectors2 = (Newtonsoft.Json.Linq.JArray?)doc2["contentVector"];
            Assert.NotNull(vectors2);
            Assert.Equal(0.4f, (float)vectors2![0]);
        }
        
        [Fact]
        public async Task UploadDocumentsAsync_ShouldThrowException_WhenApiReturnsError()
        {
            // Arrange
            var indexName = "test-index";
            var chunks = new List<ArticleChunk>
            {
                new ArticleChunk
                {
                    Id = "chunk1",
                    ArticleId = "article1",
                    ArticleTitle = "Test Article 1",
                    Content = "This is test chunk 1",
                    SectionTitle = "Introduction",
                    ArticleUrl = "https://wikipedia.org/Test_Article_1"
                }
            };
            
            var embeddings = new Dictionary<string, float[]>
            {
                ["chunk1"] = new[] { 0.1f, 0.2f, 0.3f }
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
            httpClient.BaseAddress = new Uri("https://test-search-service.search.windows.net");
            
            var searchIndexer = new AzureSearchIndexer(
                httpClient, 
                "test-api-key");

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                searchIndexer.UploadDocumentsAsync(indexName, chunks, embeddings));
        }
        
        [Fact]
        public async Task UploadDocumentsAsync_ShouldDoNothing_WhenNoChunksProvided()
        {
            // Arrange
            var indexName = "test-index";
            var chunks = new List<ArticleChunk>();
            var embeddings = new Dictionary<string, float[]>();
            
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            
            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            httpClient.BaseAddress = new Uri("https://test-search-service.search.windows.net");
            
            var searchIndexer = new AzureSearchIndexer(
                httpClient, 
                "test-api-key");

            // Act
            await searchIndexer.UploadDocumentsAsync(indexName, chunks, embeddings);

            // Assert
            mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }
    }
} 