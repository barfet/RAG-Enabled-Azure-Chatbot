using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Core.Services
{
    /// <summary>
    /// Chunks Wikipedia articles by paragraphs with consideration for semantic boundaries
    /// </summary>
    public class ParagraphTextChunker : ITextChunker
    {
        // Regex to match Wikipedia section headers (e.g., == Section Title ==)
        private static readonly Regex SectionHeaderRegex = new Regex(@"==\s*([^=]+?)\s*==", RegexOptions.Compiled);
        
        // Static readonly array for splitting content by newlines
        private static readonly char[] NewLineSeparator = new[] { '\n' };
        
        public IEnumerable<ArticleChunk> ChunkArticle(WikipediaArticle article)
        {
            // Validate parameter using ThrowIfNull
            ArgumentNullException.ThrowIfNull(article);
            
            if (string.IsNullOrWhiteSpace(article.Content))
            {
                return Enumerable.Empty<ArticleChunk>();
            }
            
            // First, handle the case of a single paragraph with no sections
            if (!article.Content.Contains('\n', StringComparison.Ordinal))
            {
                return new[] { CreateChunk(article, article.Content, string.Empty, 0) };
            }
            
            // Process the content line by line to handle both section headers and paragraphs
            var lines = article.Content.Split(NewLineSeparator, StringSplitOptions.None);
            var chunks = new List<ArticleChunk>();
            var currentSection = string.Empty;
            var currentParagraph = new List<string>();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Check if this line is a section header
                var match = SectionHeaderRegex.Match(trimmedLine);
                if (match.Success)
                {
                    // If we have accumulated content for a paragraph, add it as a chunk
                    if (currentParagraph.Count > 0)
                    {
                        chunks.Add(CreateChunk(article, string.Join("\n", currentParagraph), currentSection, chunks.Count));
                        currentParagraph.Clear();
                    }
                    
                    // Update the current section
                    currentSection = match.Groups[1].Value.Trim();
                    continue;
                }
                
                // If this is an empty line, it's a paragraph break
                if (string.IsNullOrWhiteSpace(trimmedLine) && currentParagraph.Count > 0)
                {
                    chunks.Add(CreateChunk(article, string.Join("\n", currentParagraph), currentSection, chunks.Count));
                    currentParagraph.Clear();
                    continue;
                }
                
                // Add non-empty lines to the current paragraph
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    currentParagraph.Add(trimmedLine);
                }
            }
            
            // Add the last paragraph if there's any content left
            if (currentParagraph.Count > 0)
            {
                chunks.Add(CreateChunk(article, string.Join("\n", currentParagraph), currentSection, chunks.Count));
            }
            
            return chunks;
        }
        
        private static ArticleChunk CreateChunk(WikipediaArticle article, string content, string section, int index)
        {
            return new ArticleChunk
            {
                Id = $"{article.Id}-{index}",
                ArticleId = article.Id,
                ArticleTitle = article.Title,
                SectionTitle = section,
                Content = content,
                ArticleUrl = article.Url
            };
        }
    }
} 