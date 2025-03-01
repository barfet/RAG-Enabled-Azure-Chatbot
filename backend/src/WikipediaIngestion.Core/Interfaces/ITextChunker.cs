using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Core.Interfaces
{
    /// <summary>
    /// Interface for chunking Wikipedia articles into smaller pieces
    /// </summary>
    public interface ITextChunker
    {
        /// <summary>
        /// Chunks a Wikipedia article into smaller chunks with semantic boundaries
        /// </summary>
        /// <param name="article">The Wikipedia article to chunk</param>
        /// <returns>A collection of article chunks</returns>
        IEnumerable<ArticleChunk> ChunkArticle(WikipediaArticle article);
    }
} 