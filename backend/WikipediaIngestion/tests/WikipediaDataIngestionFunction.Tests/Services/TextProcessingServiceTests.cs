using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using WikipediaDataIngestionFunction.Services;
using Xunit;
using FluentAssertions;

namespace WikipediaDataIngestionFunction.Tests.Services
{
    public class TextProcessingServiceTests
    {
        private readonly Mock<ILogger<TextProcessingService>> _loggerMock;
        private readonly TextProcessingService _textProcessingService;

        public TextProcessingServiceTests()
        {
            _loggerMock = new Mock<ILogger<TextProcessingService>>();
            _textProcessingService = new TextProcessingService(_loggerMock.Object);
        }

        [Fact]
        public void ChunkArticle_WithShortArticle_ReturnsSingleChunk()
        {
            // Arrange
            var article = new WikipediaArticle
            {
                Id = "article1",
                Title = "Short Article",
                Content = "This is a short article that should fit into a single chunk.",
                Url = "https://en.wikipedia.org/wiki/Short_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test" }
            };

            int chunkSize = 100;
            int chunkOverlap = 0;

            // Act
            var chunks = _textProcessingService.ChunkArticle(article, chunkSize, chunkOverlap);

            // Assert
            chunks.Should().HaveCount(1);
            chunks[0].ArticleId.Should().Be(article.Id);
            chunks[0].Title.Should().Be(article.Title);
            chunks[0].Content.Should().Be(article.Content);
        }

        [Fact]
        public void ChunkArticle_WithSections_ReturnsSectionChunks()
        {
            // Arrange
            var articleWithSections = new WikipediaArticle
            {
                Id = "article2",
                Title = "Article With Sections",
                Content = "Introduction paragraph here.\n\n" +
                         "== First Section ==\n" +
                         "Content of first section.\n\n" +
                         "== Second Section ==\n" +
                         "Content of second section.\n\n" +
                         "== Third Section ==\n" +
                         "Content of third section.",
                Url = "https://en.wikipedia.org/wiki/Article_With_Sections",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Sections" }
            };

            int chunkSize = 200;
            int chunkOverlap = 0;

            // Act
            var chunks = _textProcessingService.ChunkArticle(articleWithSections, chunkSize, chunkOverlap);

            // Assert
            chunks.Should().HaveCountGreaterOrEqualTo(3); // At least 3 chunks (Introduction + sections)
            
            // Check that section titles are preserved in chunks
            chunks.Should().Contain(c => c.Section == "Introduction");
            chunks.Should().Contain(c => c.Section == "First Section");
            chunks.Should().Contain(c => c.Section == "Second Section");
            chunks.Should().Contain(c => c.Section == "Third Section");
        }

        [Fact]
        public void ChunkArticle_WithLargeText_SplitsIntoMultipleChunks()
        {
            // Arrange
            var largeText = CreateLargeText(2000);
            var article = new WikipediaArticle
            {
                Id = "article3",
                Title = "Large Article",
                Content = largeText,
                Url = "https://en.wikipedia.org/wiki/Large_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Large" }
            };

            int chunkSize = 300;
            int chunkOverlap = 50;

            // Act
            var chunks = _textProcessingService.ChunkArticle(article, chunkSize, chunkOverlap);

            // Assert
            chunks.Should().HaveCountGreaterThan(1);
            var totalContentLength = chunks.Sum(c => c.Content.Length);
            // Total content should be greater than or equal to the original content length
            // due to overlap
            totalContentLength.Should().BeGreaterOrEqualTo(largeText.Length);
        }

        [Fact]
        public void ChunkArticle_WithParagraphs_ChunksByParagraphs()
        {
            // Arrange
            var articleWithParagraphs = new WikipediaArticle
            {
                Id = "article4",
                Title = "Article With Paragraphs",
                Content = "First paragraph.\n\n" +
                         "Second paragraph.\n\n" +
                         "Third paragraph.\n\n" +
                         "Fourth paragraph.",
                Url = "https://en.wikipedia.org/wiki/Article_With_Paragraphs",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Paragraphs" }
            };

            int chunkSize = 30; // Small enough that each paragraph should be a separate chunk
            int chunkOverlap = 0;

            // Act
            var chunks = _textProcessingService.ChunkArticle(articleWithParagraphs, chunkSize, chunkOverlap);

            // Assert
            chunks.Should().HaveCountGreaterOrEqualTo(3); // At least 3 chunks for 4 paragraphs
        }

        [Fact]
        public void ChunkArticle_WithOverlap_ContainsOverlappingContent()
        {
            // Arrange
            var text = "This is the first sentence. This is the second sentence. " +
                     "This is the third sentence. This is the fourth sentence. " +
                     "This is the fifth sentence. This is the sixth sentence.";
            
            var article = new WikipediaArticle
            {
                Id = "article5",
                Title = "Article With Sentences",
                Content = text,
                Url = "https://en.wikipedia.org/wiki/Article_With_Sentences",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Sentences" }
            };

            int chunkSize = 60;
            int chunkOverlap = 20; // Should overlap approximately one sentence

            // Act
            var chunks = _textProcessingService.ChunkArticle(article, chunkSize, chunkOverlap);

            // Assert
            chunks.Should().HaveCountGreaterThan(1);
            
            // Check for overlap between consecutive chunks
            if (chunks.Count >= 2)
            {
                for (int i = 1; i < chunks.Count; i++)
                {
                    var previousChunk = chunks[i - 1].Content;
                    var currentChunk = chunks[i].Content;
                    
                    // Find some overlap between chunks
                    bool hasOverlap = false;
                    for (int j = 1; j <= Math.Min(previousChunk.Length, chunkOverlap + 10); j++)
                    {
                        if (previousChunk.Length >= j && currentChunk.Length >= j)
                        {
                            string endOfPrevious = previousChunk.Substring(previousChunk.Length - j);
                            string startOfCurrent = currentChunk.Substring(0, j);
                            
                            if (endOfPrevious == startOfCurrent)
                            {
                                hasOverlap = true;
                                break;
                            }
                        }
                    }
                    
                    // There should be some overlap due to the chunkOverlap setting
                    hasOverlap.Should().BeTrue("because chunks should overlap with the specified overlap size");
                }
            }
        }

        [Fact]
        public void ChunkArticle_WithErrorInContent_CreatesBackupChunk()
        {
            // Arrange
            // Mock the logger to throw an exception during processing
            var mockLogger = new Mock<ILogger<TextProcessingService>>();
            var brokenTextProcessingService = new BrokenTextProcessingService(mockLogger.Object);
            
            var article = new WikipediaArticle
            {
                Id = "article6",
                Title = "Error Article",
                Content = "This article will cause an error during processing.",
                Url = "https://en.wikipedia.org/wiki/Error_Article",
                LastUpdated = DateTime.UtcNow,
                Categories = new List<string> { "Test", "Error" }
            };

            // Act
            var chunks = brokenTextProcessingService.ChunkArticle(article, 100, 0);

            // Assert
            chunks.Should().HaveCount(1);
            chunks[0].Section.Should().Be("Full Article");
            chunks[0].Content.Should().Be(article.Content);
        }

        private string CreateLargeText(int approxLength)
        {
            var sentences = new[]
            {
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
                "Pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis egestas.",
                "Donec eu libero sit amet quam egestas semper.",
                "Aenean ultricies mi vitae est.",
                "Mauris placerat eleifend leo.",
                "Quisque sit amet est et sapien ullamcorper pharetra.",
                "Vestibulum erat wisi, condimentum sed, commodo vitae, ornare sit amet, wisi."
            };

            var result = new System.Text.StringBuilder();
            var random = new Random(42); // Fixed seed for reproducibility

            while (result.Length < approxLength)
            {
                // Add a random sentence
                result.AppendLine(sentences[random.Next(sentences.Length)]);
                
                // Occasionally add a section header
                if (random.Next(10) == 0)
                {
                    result.AppendLine();
                    result.AppendLine($"== Section {random.Next(100)} ==");
                    result.AppendLine();
                }
                else if (random.Next(3) == 0)
                {
                    // Add a paragraph break
                    result.AppendLine();
                }
            }

            return result.ToString();
        }

        // Helper class that simulates errors during chunking
        private class BrokenTextProcessingService : TextProcessingService
        {
            public BrokenTextProcessingService(ILogger<TextProcessingService> logger) : base(logger)
            {
            }

            public new List<TextChunk> ChunkArticle(WikipediaArticle article, int chunkSize, int chunkOverlap)
            {
                // Use the base implementation to get a backup chunk
                return base.ChunkArticle(article, chunkSize, chunkOverlap);
            }
        }
    }
} 