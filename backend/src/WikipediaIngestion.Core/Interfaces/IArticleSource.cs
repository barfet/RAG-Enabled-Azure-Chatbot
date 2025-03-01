using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Core.Interfaces
{
    /// <summary>
    /// Interface for retrieving Wikipedia articles from a source
    /// </summary>
    public interface IArticleSource
    {
        /// <summary>
        /// Retrieves a batch of Wikipedia articles
        /// </summary>
        /// <param name="limit">Maximum number of articles to retrieve</param>
        /// <param name="offset">Starting position in the dataset</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A collection of Wikipedia articles</returns>
        Task<IEnumerable<WikipediaArticle>> GetArticlesAsync(int limit, int offset, CancellationToken cancellationToken = default);
    }
} 