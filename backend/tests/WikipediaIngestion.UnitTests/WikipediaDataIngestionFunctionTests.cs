using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Models;
using WikipediaIngestion.Functions;
using Xunit;

namespace WikipediaIngestion.UnitTests
{
    public class WikipediaDataIngestionFunctionTests
    {
        [Fact]
        public async Task ProcessWikipediaArticlesAsync_ShouldProcessArticlesAndIndexThem()
        {
            // Arrange
            var indexName = "wikipedia-index";
            var limit = 10;
            var offset = 0;
            
            // Mock articles
            var articles = new List<WikipediaArticle>
            {
                new WikipediaArticle
                {
                    Id = "article1",
                    Title = "Test Article 1",
                    Content = "This is the introduction.\n\n== History ==\nThis is the history section.",
                    Url = "https://wikipedia.org/wiki/Test_Article_1",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Category1" }
                }
            };
            
            // Mock chunks
            var chunks = new List<ArticleChunk>
            {
                new ArticleChunk
                {
                    Id = "article1-0",
                    ArticleId = "article1",
                    ArticleTitle = "Test Article 1",
                    Content = "This is the introduction.",
                    SectionTitle = string.Empty,
                    ArticleUrl = "https://wikipedia.org/wiki/Test_Article_1"
                },
                new ArticleChunk
                {
                    Id = "article1-1",
                    ArticleId = "article1",
                    ArticleTitle = "Test Article 1",
                    Content = "This is the history section.",
                    SectionTitle = "History",
                    ArticleUrl = "https://wikipedia.org/wiki/Test_Article_1"
                }
            };
            
            // Mock embeddings
            var embeddings = new Dictionary<string, float[]>
            {
                ["article1-0"] = new[] { 0.1f, 0.2f, 0.3f },
                ["article1-1"] = new[] { 0.4f, 0.5f, 0.6f }
            };
            
            // Mock dependencies
            var mockArticleSource = new Mock<IArticleSource>();
            var mockTextChunker = new Mock<ITextChunker>();
            var mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();
            var mockSearchIndexer = new Mock<ISearchIndexer>();
            var mockLogger = new Mock<ILogger<WikipediaDataIngestionFunction>>();
            
            // Setup mocks
            mockArticleSource
                .Setup(s => s.GetArticlesAsync(limit, offset, It.IsAny<CancellationToken>()))
                .ReturnsAsync(articles);
            
            mockTextChunker
                .Setup(c => c.ChunkArticle(It.IsAny<WikipediaArticle>()))
                .Returns<WikipediaArticle>(article => chunks.Where(c => c.ArticleId == article.Id));
            
            mockEmbeddingGenerator
                .Setup(g => g.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<ArticleChunk>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(embeddings);
            
            mockSearchIndexer
                .Setup(i => i.CreateIndexIfNotExistsAsync(indexName, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            mockSearchIndexer
                .Setup(i => i.UploadDocumentsAsync(indexName, It.IsAny<IEnumerable<ArticleChunk>>(), embeddings, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Create function
            var function = new WikipediaDataIngestionFunction(
                mockArticleSource.Object,
                mockTextChunker.Object,
                mockEmbeddingGenerator.Object,
                mockSearchIndexer.Object,
                mockLogger.Object);
            
            // Mock function context
            var context = new Mock<FunctionContext>();
            
            // Act
            await function.ProcessWikipediaArticlesAsync(context.Object);
            
            // Assert
            mockArticleSource.Verify(s => s.GetArticlesAsync(limit, offset, It.IsAny<CancellationToken>()), Times.Once);
            mockTextChunker.Verify(c => c.ChunkArticle(It.IsAny<WikipediaArticle>()), Times.Once);
            mockEmbeddingGenerator.Verify(g => g.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<ArticleChunk>>(), It.IsAny<CancellationToken>()), Times.Once);
            mockSearchIndexer.Verify(i => i.CreateIndexIfNotExistsAsync(indexName, It.IsAny<CancellationToken>()), Times.Once);
            mockSearchIndexer.Verify(i => i.UploadDocumentsAsync(indexName, It.IsAny<IEnumerable<ArticleChunk>>(), embeddings, It.IsAny<CancellationToken>()), Times.Once);
        }
        
        [Fact]
        public async Task ProcessWikipediaArticlesAsync_ShouldLogProgress()
        {
            // Arrange
            var indexName = "wikipedia-index";
            var limit = 10;
            var offset = 0;
            
            // Mock articles
            var articles = new List<WikipediaArticle>
            {
                new WikipediaArticle
                {
                    Id = "article1",
                    Title = "Test Article 1",
                    Content = "Test content",
                    Url = "https://wikipedia.org/wiki/Test_Article_1",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<string> { "Category1" }
                }
            };
            
            // Mock chunks
            var chunks = new List<ArticleChunk>
            {
                new ArticleChunk
                {
                    Id = "article1-0",
                    ArticleId = "article1",
                    ArticleTitle = "Test Article 1",
                    Content = "Test content",
                    SectionTitle = string.Empty,
                    ArticleUrl = "https://wikipedia.org/wiki/Test_Article_1"
                }
            };
            
            // Mock embeddings
            var embeddings = new Dictionary<string, float[]>
            {
                ["article1-0"] = new[] { 0.1f, 0.2f, 0.3f }
            };
            
            // Mock dependencies
            var mockArticleSource = new Mock<IArticleSource>();
            var mockTextChunker = new Mock<ITextChunker>();
            var mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();
            var mockSearchIndexer = new Mock<ISearchIndexer>();
            var mockLogger = new Mock<ILogger<WikipediaDataIngestionFunction>>();
            
            // Setup mocks
            mockArticleSource
                .Setup(s => s.GetArticlesAsync(limit, offset, It.IsAny<CancellationToken>()))
                .ReturnsAsync(articles);
            
            mockTextChunker
                .Setup(c => c.ChunkArticle(It.IsAny<WikipediaArticle>()))
                .Returns<WikipediaArticle>(article => chunks.Where(c => c.ArticleId == article.Id));
            
            mockEmbeddingGenerator
                .Setup(g => g.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<ArticleChunk>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(embeddings);
            
            mockSearchIndexer
                .Setup(i => i.CreateIndexIfNotExistsAsync(indexName, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            mockSearchIndexer
                .Setup(i => i.UploadDocumentsAsync(indexName, It.IsAny<IEnumerable<ArticleChunk>>(), embeddings, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Create function
            var function = new WikipediaDataIngestionFunction(
                mockArticleSource.Object,
                mockTextChunker.Object,
                mockEmbeddingGenerator.Object,
                mockSearchIndexer.Object,
                mockLogger.Object);
            
            // Mock function context
            var context = new Mock<FunctionContext>();
            
            // Act
            await function.ProcessWikipediaArticlesAsync(context.Object);
            
            // Assert
            // Verify that logging happened - at least function start and completion
            mockLogger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Information),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting Wikipedia data ingestion")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
            
            mockLogger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Information),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Completed Wikipedia data ingestion")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }
    }
} 