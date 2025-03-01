using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WikipediaDataIngestionFunction.Services;
using Xunit;
using FluentAssertions;

namespace WikipediaDataIngestionFunction.Tests.Services
{
    public class AzureBlobStorageServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<ILogger<AzureBlobStorageService>> _loggerMock;
        private readonly Mock<BlobContainerClient> _containerClientMock;
        private readonly Mock<BlobClient> _blobClientMock;

        public AzureBlobStorageServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<AzureBlobStorageService>>();
            _containerClientMock = new Mock<BlobContainerClient>();
            _blobClientMock = new Mock<BlobClient>();

            // Setup configuration
            _configMock.Setup(c => c["Storage__ConnectionString"]).Returns("UseDevelopmentStorage=true");
            _configMock.Setup(c => c["Storage__ContainerName"]).Returns("wikipedia-data");
        }

        [Fact]
        public async Task SaveRawArticleAsync_CreatesContainerIfNotExists()
        {
            // Arrange
            _containerClientMock
                .Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Response<BlobContainerInfo>>().Object);

            _containerClientMock
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(_blobClientMock.Object);

            _blobClientMock
                .Setup(c => c.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

            var article = new WikipediaArticle
            {
                Id = "article1",
                Title = "Test Article",
                Content = "Test content",
                Url = "https://en.wikipedia.org/wiki/Test_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test" }
            };

            var service = new TestableAzureBlobStorageService(
                _configMock.Object,
                _loggerMock.Object,
                _containerClientMock.Object);

            // Act
            await service.SaveRawArticleAsync(article);

            // Assert
            _containerClientMock.Verify(
                c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _containerClientMock.Verify(
                c => c.GetBlobClient($"articles/{article.Id}.json"),
                Times.Once);

            _blobClientMock.Verify(
                c => c.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveRawArticleAsync_SerializesArticleCorrectly()
        {
            // Arrange
            Stream capturedStream = null;

            _containerClientMock
                .Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Response<BlobContainerInfo>>().Object);

            _containerClientMock
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(_blobClientMock.Object);

            _blobClientMock
                .Setup(c => c.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, bool, CancellationToken>((stream, _, _) =>
                {
                    capturedStream = new MemoryStream();
                    stream.CopyTo(capturedStream);
                    capturedStream.Position = 0;
                })
                .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

            var article = new WikipediaArticle
            {
                Id = "article1",
                Title = "Test Article",
                Content = "Test content",
                Url = "https://en.wikipedia.org/wiki/Test_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test" }
            };

            var service = new TestableAzureBlobStorageService(
                _configMock.Object,
                _loggerMock.Object,
                _containerClientMock.Object);

            // Act
            await service.SaveRawArticleAsync(article);

            // Assert
            capturedStream.Should().NotBeNull();
            
            // Deserialize the stream and verify it matches the original article
            capturedStream.Position = 0;
            var deserializedArticle = await JsonSerializer.DeserializeAsync<WikipediaArticle>(capturedStream);
            
            deserializedArticle.Should().NotBeNull();
            deserializedArticle.Id.Should().Be(article.Id);
            deserializedArticle.Title.Should().Be(article.Title);
            deserializedArticle.Content.Should().Be(article.Content);
            deserializedArticle.Url.Should().Be(article.Url);
            deserializedArticle.Categories.Should().BeEquivalentTo(article.Categories);
        }

        [Fact]
        public async Task GetRawArticlesAsync_ReturnsEmptyListWhenNoArticles()
        {
            // Arrange
            _containerClientMock
                .Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Response<BlobContainerInfo>>().Object);

            // No blobs returned from the container
            var emptyList = new List<BlobItem>();
            var pageable = Pageable<BlobItem>.FromPages(new[] { Page<BlobItem>.FromValues(emptyList, null, new Mock<Response>().Object) });
            
            _containerClientMock
                .Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(AsyncPageable<BlobItem>.FromPages(pageable.AsPages()));

            var service = new TestableAzureBlobStorageService(
                _configMock.Object,
                _loggerMock.Object,
                _containerClientMock.Object);

            // Act
            var result = await service.GetRawArticlesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetRawArticlesAsync_ReturnsDeserializedArticles()
        {
            // Arrange
            _containerClientMock
                .Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

            // Create mock blob items
            var blobItems = new List<BlobItem>
            {
                BlobsModelFactory.BlobItem("articles/article1.json", false, BlobItemProperties.BlobItemPropertiesDefaults),
                BlobsModelFactory.BlobItem("articles/article2.json", false, BlobItemProperties.BlobItemPropertiesDefaults)
            };
            
            var pageable = Pageable<BlobItem>.FromPages(new[] { Page<BlobItem>.FromValues(blobItems, null, new Mock<Response>().Object) });
            
            _containerClientMock
                .Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(AsyncPageable<BlobItem>.FromPages(pageable.AsPages()));

            // Set up blob client mock for each article
            var blobClient1 = new Mock<BlobClient>();
            var blobClient2 = new Mock<BlobClient>();

            _containerClientMock
                .Setup(c => c.GetBlobClient("articles/article1.json"))
                .Returns(blobClient1.Object);
                
            _containerClientMock
                .Setup(c => c.GetBlobClient("articles/article2.json"))
                .Returns(blobClient2.Object);

            // Mock download responses with serialized articles
            var article1 = new WikipediaArticle
            {
                Id = "article1",
                Title = "Test Article 1",
                Content = "Test content 1"
            };
            
            var article2 = new WikipediaArticle
            {
                Id = "article2",
                Title = "Test Article 2",
                Content = "Test content 2"
            };

            // Set up the first blob to return article1
            blobClient1
                .Setup(c => c.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>(async (stream, _) =>
                {
                    await JsonSerializer.SerializeAsync(stream, article1);
                    stream.Position = 0;
                })
                .ReturnsAsync(new Mock<Response>().Object);

            // Set up the second blob to return article2
            blobClient2
                .Setup(c => c.DownloadToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>(async (stream, _) =>
                {
                    await JsonSerializer.SerializeAsync(stream, article2);
                    stream.Position = 0;
                })
                .ReturnsAsync(new Mock<Response>().Object);

            var service = new TestableAzureBlobStorageService(
                _configMock.Object,
                _loggerMock.Object,
                _containerClientMock.Object);

            // Act
            var result = await service.GetRawArticlesAsync();

            // Assert
            result.Should().HaveCount(2);
            result[0].Id.Should().Be("article1");
            result[0].Title.Should().Be("Test Article 1");
            result[0].Content.Should().Be("Test content 1");
            
            result[1].Id.Should().Be("article2");
            result[1].Title.Should().Be("Test Article 2");
            result[1].Content.Should().Be("Test content 2");
        }

        [Fact]
        public async Task EnsureContainerExistsAsync_HandlesExceptions()
        {
            // Arrange
            _containerClientMock
                .Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test error"));

            var service = new TestableAzureBlobStorageService(
                _configMock.Object,
                _loggerMock.Object,
                _containerClientMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => service.SaveRawArticleAsync(new WikipediaArticle()));
            
            // Verify that the error is logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error creating blob container")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        // Testable version of the service to allow dependency injection
        private class TestableAzureBlobStorageService : AzureBlobStorageService
        {
            public TestableAzureBlobStorageService(
                IConfiguration configuration,
                ILogger<AzureBlobStorageService> logger,
                BlobContainerClient containerClient)
                : base(configuration, logger)
            {
                // Use reflection to set the private field with our mock
                var containerClientField = typeof(AzureBlobStorageService)
                    .GetField("_containerClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                containerClientField.SetValue(this, containerClient);
            }
        }
    }
} 