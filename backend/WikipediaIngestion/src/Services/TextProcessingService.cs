using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace WikipediaDataIngestionFunction.Services
{
    public class TextProcessingService : ITextProcessingService
    {
        private readonly ILogger<TextProcessingService> _logger;
        
        public TextProcessingService(ILogger<TextProcessingService> logger)
        {
            _logger = logger;
        }
        
        public List<TextChunk> ChunkArticle(WikipediaArticle article, int chunkSize, int chunkOverlap)
        {
            _logger.LogInformation("Chunking article: {Title}", article.Title);
            var chunks = new List<TextChunk>();
            
            try
            {
                // First, try to split by sections if they exist
                var sections = SplitBySection(article.Content);
                
                if (sections.Count > 1)
                {
                    _logger.LogInformation("Article split into {Count} sections", sections.Count);
                    int sectionIndex = 0;
                    
                    foreach (var section in sections)
                    {
                        // If section is very large, split it further
                        if (section.Value.Length > chunkSize * 1.5)
                        {
                            var sectionChunks = SplitTextBySize(section.Value, chunkSize, chunkOverlap);
                            for (int i = 0; i < sectionChunks.Count; i++)
                            {
                                chunks.Add(CreateChunk(
                                    article, 
                                    sectionChunks[i], 
                                    $"{section.Key} (Part {i + 1}/{sectionChunks.Count})", 
                                    sectionIndex * 100 + i
                                ));
                            }
                        }
                        else
                        {
                            chunks.Add(CreateChunk(article, section.Value, section.Key, sectionIndex));
                        }
                        
                        sectionIndex++;
                    }
                }
                else
                {
                    // If no sections, split by paragraphs
                    var paragraphs = SplitByParagraph(article.Content);
                    
                    if (paragraphs.Count > 1)
                    {
                        _logger.LogInformation("Article split into {Count} paragraphs", paragraphs.Count);
                        
                        // Combine paragraphs into chunks of appropriate size
                        var currentChunk = new List<string>();
                        int currentLength = 0;
                        int chunkIndex = 0;
                        
                        foreach (var paragraph in paragraphs)
                        {
                            // If adding this paragraph would exceed chunk size, create a new chunk
                            if (currentLength + paragraph.Length > chunkSize && currentChunk.Count > 0)
                            {
                                chunks.Add(CreateChunk(
                                    article, 
                                    string.Join(Environment.NewLine + Environment.NewLine, currentChunk), 
                                    $"Chunk {chunkIndex + 1}", 
                                    chunkIndex
                                ));
                                
                                // Start a new chunk, possibly with overlap
                                if (chunkOverlap > 0 && currentChunk.Count > 1)
                                {
                                    // Add the last paragraph from previous chunk for context overlap
                                    currentChunk = new List<string> { currentChunk.Last() };
                                    currentLength = currentChunk.Last().Length;
                                }
                                else
                                {
                                    currentChunk = new List<string>();
                                    currentLength = 0;
                                }
                                
                                chunkIndex++;
                            }
                            
                            currentChunk.Add(paragraph);
                            currentLength += paragraph.Length;
                        }
                        
                        // Add the remaining paragraphs as the last chunk
                        if (currentChunk.Count > 0)
                        {
                            chunks.Add(CreateChunk(
                                article, 
                                string.Join(Environment.NewLine + Environment.NewLine, currentChunk), 
                                $"Chunk {chunkIndex + 1}", 
                                chunkIndex
                            ));
                        }
                    }
                    else
                    {
                        // If no paragraphs, split by size
                        var textChunks = SplitTextBySize(article.Content, chunkSize, chunkOverlap);
                        
                        for (int i = 0; i < textChunks.Count; i++)
                        {
                            chunks.Add(CreateChunk(
                                article, 
                                textChunks[i], 
                                $"Chunk {i + 1}/{textChunks.Count}", 
                                i
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error chunking article {Title}", article.Title);
                
                // Fallback: create a single chunk with the whole article
                chunks.Add(CreateChunk(article, article.Content, "Full Article", 0));
            }
            
            _logger.LogInformation("Created {Count} chunks for article: {Title}", chunks.Count, article.Title);
            return chunks;
        }
        
        private TextChunk CreateChunk(WikipediaArticle article, string content, string section, int index)
        {
            return new TextChunk
            {
                Id = $"{article.Id}-{index}",
                ArticleId = article.Id,
                Title = article.Title,
                Content = content,
                Section = section,
                Url = article.Url,
                LastUpdated = article.LastUpdated,
                Categories = article.Categories
            };
        }
        
        private Dictionary<string, string> SplitBySection(string text)
        {
            var sections = new Dictionary<string, string>();
            
            // Pattern for section headers (== Title == or === Subtitle ===)
            var sectionPattern = new Regex(@"(^|\n)==+\s*([^=]+?)\s*==+", RegexOptions.Multiline);
            var matches = sectionPattern.Matches(text);
            
            if (matches.Count == 0)
            {
                sections.Add("Main Content", text);
                return sections;
            }
            
            // Add content before the first section
            if (matches[0].Index > 0)
            {
                sections.Add("Introduction", text.Substring(0, matches[0].Index).Trim());
            }
            
            // Process each section
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var sectionTitle = match.Groups[2].Value.Trim();
                int startIdx = match.Index + match.Length;
                int endIdx = (i < matches.Count - 1) ? matches[i + 1].Index : text.Length;
                var sectionContent = text.Substring(startIdx, endIdx - startIdx).Trim();
                
                sections.Add(sectionTitle, sectionContent);
            }
            
            return sections;
        }
        
        private List<string> SplitByParagraph(string text)
        {
            return text.Split(new[] { Environment.NewLine + Environment.NewLine, "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
        }
        
        private List<string> SplitTextBySize(string text, int chunkSize, int chunkOverlap)
        {
            var chunks = new List<string>();
            
            if (text.Length <= chunkSize)
            {
                chunks.Add(text);
                return chunks;
            }
            
            int startIndex = 0;
            while (startIndex < text.Length)
            {
                // Calculate end index for this chunk
                int endIndex = Math.Min(startIndex + chunkSize, text.Length);
                
                // Try to find a good break point (end of sentence)
                if (endIndex < text.Length)
                {
                    int sentenceEndIndex = FindSentenceEnd(text, endIndex);
                    if (sentenceEndIndex > 0)
                    {
                        endIndex = sentenceEndIndex;
                    }
                }
                
                // Extract the chunk
                string chunk = text.Substring(startIndex, endIndex - startIndex).Trim();
                chunks.Add(chunk);
                
                // Move start index for next chunk, accounting for overlap
                startIndex = endIndex - chunkOverlap;
                if (startIndex < 0 || startIndex >= text.Length - 1)
                {
                    break;
                }
            }
            
            return chunks;
        }
        
        private int FindSentenceEnd(string text, int approximateIndex)
        {
            // Look for common sentence terminators within a reasonable range
            int searchRange = 100; // Look up to 100 chars ahead
            int maxIndex = Math.Min(approximateIndex + searchRange, text.Length);
            
            for (int i = approximateIndex; i < maxIndex; i++)
            {
                if (i < text.Length - 1 && (text[i] == '.' || text[i] == '!' || text[i] == '?') && char.IsWhiteSpace(text[i + 1]))
                {
                    return i + 1; // Include the terminator and move to the whitespace
                }
            }
            
            // If we couldn't find a sentence break, look for paragraph or line breaks
            for (int i = approximateIndex; i < maxIndex; i++)
            {
                if (i < text.Length - 1 && text[i] == '\n')
                {
                    return i + 1;
                }
            }
            
            // If no suitable break point found, just return the original index
            return approximateIndex;
        }
    }
} 