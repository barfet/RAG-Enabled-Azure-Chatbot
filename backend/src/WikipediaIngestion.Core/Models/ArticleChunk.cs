namespace WikipediaIngestion.Core.Models
{
    /// <summary>
    /// Represents a chunk of text from a Wikipedia article
    /// </summary>
    public class ArticleChunk
    {
        /// <summary>
        /// Unique identifier for the chunk
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// ID of the Wikipedia article this chunk belongs to
        /// </summary>
        public string ArticleId { get; set; } = string.Empty;
        
        /// <summary>
        /// Title of the Wikipedia article this chunk belongs to
        /// </summary>
        public string ArticleTitle { get; set; } = string.Empty;
        
        /// <summary>
        /// Text content of the chunk
        /// </summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// Title of the section this chunk belongs to (if any)
        /// </summary>
        public string SectionTitle { get; set; } = string.Empty;
        
        /// <summary>
        /// URL to the original Wikipedia article
        /// </summary>
        public string ArticleUrl { get; set; } = string.Empty;
    }
} 