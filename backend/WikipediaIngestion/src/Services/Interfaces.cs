using System.Collections.Generic;
using System.Threading.Tasks;

namespace WikipediaDataIngestionFunction.Services
{
    // Model classes
    public class WikipediaArticle
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
    }

    public class TextChunk
    {
        public string Id { get; set; } = string.Empty;
        public string ArticleId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public float[] ContentVector { get; set; } = Array.Empty<float>();
    }

    // Service interfaces
    public interface IWikipediaService
    {
        Task<List<WikipediaArticle>> GetArticlesAsync(int maxArticles);
    }

    public interface ITextProcessingService
    {
        List<TextChunk> ChunkArticle(WikipediaArticle article, int chunkSize, int chunkOverlap);
    }

    public interface IEmbeddingService
    {
        Task<List<TextChunk>> GenerateEmbeddingsAsync(List<TextChunk> chunks);
        Task<float[]> GenerateEmbeddingAsync(string text);
    }

    public interface ISearchIndexService
    {
        Task CreateIndexIfNotExistsAsync();
        Task IndexChunksAsync(List<TextChunk> chunks);
    }

    public interface IStorageService
    {
        Task SaveRawArticleAsync(WikipediaArticle article);
        Task<List<WikipediaArticle>> GetRawArticlesAsync();
    }
} 