using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Core.Interfaces
{
    /// <summary>
    /// Interface for indexing article chunks in a search service
    /// </summary>
    public interface ISearchIndexer
    {
        /// <summary>
        /// Creates a search index if it doesn't exist
        /// </summary>
        /// <param name="indexName">Name of the index to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task CreateIndexIfNotExistsAsync(string indexName, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Indexes a batch of article chunks in the search index
        /// </summary>
        /// <param name="indexName">Name of the index to add chunks to</param>
        /// <param name="chunks">The article chunks to index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task IndexChunksAsync(string indexName, IEnumerable<ArticleChunk> chunks, CancellationToken cancellationToken = default);
    }
} 