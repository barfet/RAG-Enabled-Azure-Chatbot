namespace WikipediaIngestion.Core.Models
{
    /// <summary>
    /// Represents a Wikipedia article retrieved from Hugging Face dataset
    /// </summary>
    public class WikipediaArticle
    {
        /// <summary>
        /// Unique identifier for the article
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Title of the Wikipedia article
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Full text content of the article
        /// </summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// URL to the original Wikipedia article
        /// </summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// Last time the article was updated
        /// </summary>
        public DateTime LastUpdated { get; set; }
        
        /// <summary>
        /// Categories associated with the article
        /// </summary>
        public List<string> Categories { get; set; } = new List<string>();
    }
} 