using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Models;
using WikipediaIngestion.Core.Services;
using Xunit;

namespace WikipediaIngestion.UnitTests
{
    public class TextChunkerTests
    {
        [Fact]
        public void ChunkArticle_ShouldReturnEmptyCollection_WhenArticleContentIsEmpty()
        {
            // Arrange
            var article = new WikipediaArticle
            {
                Id = "test-id",
                Title = "Test Article",
                Content = string.Empty,
                Url = "https://wikipedia.org/Test_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Article" }
            };
            
            var chunker = new ParagraphTextChunker();
            
            // Act
            var chunks = chunker.ChunkArticle(article).ToList();
            
            // Assert
            Assert.Empty(chunks);
        }
        
        [Fact]
        public void ChunkArticle_ShouldCreateSingleChunk_WhenContentHasNoParagraphs()
        {
            // Arrange
            var article = new WikipediaArticle
            {
                Id = "test-id",
                Title = "Test Article",
                Content = "This is a test article with a single paragraph.",
                Url = "https://wikipedia.org/Test_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Article" }
            };
            
            var chunker = new ParagraphTextChunker();
            
            // Act
            var chunks = chunker.ChunkArticle(article).ToList();
            
            // Assert
            Assert.Single(chunks);
            Assert.Equal(article.Content, chunks[0].Content);
            Assert.Equal(article.Title, chunks[0].ArticleTitle);
            Assert.Equal(article.Url, chunks[0].ArticleUrl);
            Assert.Equal($"{article.Id}-0", chunks[0].Id);
            Assert.Equal(article.Id, chunks[0].ArticleId);
            Assert.Empty(chunks[0].SectionTitle);
        }
        
        [Fact]
        public void ChunkArticle_ShouldCreateMultipleChunks_WhenContentHasMultipleParagraphs()
        {
            // Arrange
            var article = new WikipediaArticle
            {
                Id = "test-id",
                Title = "Test Article",
                Content = "This is the first paragraph.\n\nThis is the second paragraph.\n\nThis is the third paragraph.",
                Url = "https://wikipedia.org/Test_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Article" }
            };
            
            var chunker = new ParagraphTextChunker();
            
            // Act
            var chunks = chunker.ChunkArticle(article).ToList();
            
            // Assert
            Assert.Equal(3, chunks.Count);
            
            Assert.Equal("This is the first paragraph.", chunks[0].Content);
            Assert.Equal("This is the second paragraph.", chunks[1].Content);
            Assert.Equal("This is the third paragraph.", chunks[2].Content);
            
            Assert.Equal($"{article.Id}-0", chunks[0].Id);
            Assert.Equal($"{article.Id}-1", chunks[1].Id);
            Assert.Equal($"{article.Id}-2", chunks[2].Id);
            
            // All chunks should inherit metadata from the original article
            foreach (var chunk in chunks)
            {
                Assert.Equal(article.Title, chunk.ArticleTitle);
                Assert.Equal(article.Url, chunk.ArticleUrl);
                Assert.Equal(article.Id, chunk.ArticleId);
                Assert.Empty(chunk.SectionTitle);
            }
        }
        
        [Fact]
        public void ChunkArticle_ShouldIdentifySections_WhenContentHasHeadings()
        {
            // Arrange
            var article = new WikipediaArticle
            {
                Id = "test-id",
                Title = "Test Article",
                Content = "This is the introduction.\n\n== History ==\nThis is the history section.\n\n== Geography ==\nThis is the geography section.",
                Url = "https://wikipedia.org/Test_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Article" }
            };
            
            var chunker = new ParagraphTextChunker();
            
            // Act
            var chunks = chunker.ChunkArticle(article).ToList();
            
            // Assert
            Assert.Equal(3, chunks.Count);
            
            Assert.Equal("This is the introduction.", chunks[0].Content);
            Assert.Empty(chunks[0].SectionTitle);
            
            Assert.Equal("This is the history section.", chunks[1].Content);
            Assert.Equal("History", chunks[1].SectionTitle);
            
            Assert.Equal("This is the geography section.", chunks[2].Content);
            Assert.Equal("Geography", chunks[2].SectionTitle);
        }
        
        [Fact]
        public void ChunkArticle_ShouldHandleMultipleParagraphsInSections()
        {
            // Arrange
            var article = new WikipediaArticle
            {
                Id = "test-id",
                Title = "Test Article",
                Content = "This is the introduction.\n\n== History ==\nThis is the first paragraph of history.\n\nThis is the second paragraph of history.\n\n== Geography ==\nThis is the geography section.",
                Url = "https://wikipedia.org/Test_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Article" }
            };
            
            var chunker = new ParagraphTextChunker();
            
            // Act
            var chunks = chunker.ChunkArticle(article).ToList();
            
            // Assert
            Assert.Equal(4, chunks.Count);
            
            Assert.Equal("This is the introduction.", chunks[0].Content);
            Assert.Empty(chunks[0].SectionTitle);
            
            Assert.Equal("This is the first paragraph of history.", chunks[1].Content);
            Assert.Equal("History", chunks[1].SectionTitle);
            
            Assert.Equal("This is the second paragraph of history.", chunks[2].Content);
            Assert.Equal("History", chunks[2].SectionTitle);
            
            Assert.Equal("This is the geography section.", chunks[3].Content);
            Assert.Equal("Geography", chunks[3].SectionTitle);
        }
    }
} 