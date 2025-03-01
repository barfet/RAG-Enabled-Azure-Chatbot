using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Core.Interfaces
{
    /// <summary>
    /// Interface for generating vector embeddings for article chunks
    /// </summary>
    public interface IEmbeddingGenerator
    {
        /// <summary>
        /// Generates embeddings for a collection of article chunks
        /// </summary>
        /// <param name="chunks">The article chunks to generate embeddings for</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A dictionary mapping chunk IDs to their vector embeddings</returns>
        Task<Dictionary<string, float[]>> GenerateEmbeddingsAsync(
            IEnumerable<ArticleChunk> chunks, 
            CancellationToken cancellationToken = default);
    }
} 