namespace WikipediaIngestion.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents a Wikipedia article retrieved from Hugging Face dataset
    /// </summary>
    public class WikipediaArticle
    {
        private readonly List<string> _categories = new List<string>();

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
        public Uri Url { get; set; } = new Uri("https://en.wikipedia.org/");
        
        /// <summary>
        /// Last time the article was updated
        /// </summary>
        public DateTime LastUpdated { get; set; }
        
        /// <summary>
        /// Categories associated with the article
        /// </summary>
        public IReadOnlyCollection<string> Categories => _categories.AsReadOnly();

        /// <summary>
        /// Adds a category to the article
        /// </summary>
        public void AddCategory(string category)
        {
            if (!string.IsNullOrEmpty(category))
            {
                _categories.Add(category);
            }
        }

        /// <summary>
        /// Adds multiple categories to the article
        /// </summary>
        public void AddCategories(IEnumerable<string> categories)
        {
            if (categories != null)
            {
                _categories.AddRange(categories);
            }
        }
    }
} 