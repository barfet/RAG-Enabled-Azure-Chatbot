using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Core.Interfaces
{
    /// <summary>
    /// Interface for indexing article chunks in a search service
    /// </summary>
    public interface ISearchIndexer
    {
        /// <summary>
        /// Creates a search index if it doesn't already exist
        /// </summary>
        /// <param name="indexName">The name of the index to create</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task CreateIndexIfNotExistsAsync(string indexName, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Uploads article chunks with their embeddings to the search index
        /// </summary>
        /// <param name="indexName">The name of the index to upload to</param>
        /// <param name="chunks">The article chunks to upload</param>
        /// <param name="embeddings">Dictionary mapping chunk IDs to their vector embeddings</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task UploadDocumentsAsync(
            string indexName,
            IEnumerable<ArticleChunk> chunks,
            Dictionary<string, float[]> embeddings,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Deletes a search index if it exists
        /// </summary>
        /// <param name="indexName">The name of the index to delete</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task DeleteIndexIfExistsAsync(string indexName, CancellationToken cancellationToken = default);
    }
} 