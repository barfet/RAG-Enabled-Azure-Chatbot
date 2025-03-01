using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using WikipediaIngestion.Core.Interfaces;
using WikipediaIngestion.Core.Models;

namespace WikipediaIngestion.Functions
{
    /// <summary>
    /// Azure Function for ingesting Wikipedia data into Azure AI Search
    /// </summary>
    public class WikipediaDataIngestionFunction
    {
        private readonly IArticleSource _articleSource;
        private readonly ITextChunker _textChunker;
        private readonly IEmbeddingGenerator _embeddingGenerator;
        private readonly ISearchIndexer _searchIndexer;
        private readonly ILogger<WikipediaDataIngestionFunction> _logger;
        private readonly string _indexName = "wikipedia-index";
        private readonly int _limit = 10; // Limit for initial testing
        private readonly int _offset = 0;
        
        /// <summary>
        /// Creates a new instance of the WikipediaDataIngestionFunction
        /// </summary>
        public WikipediaDataIngestionFunction(
            IArticleSource articleSource,
            ITextChunker textChunker,
            IEmbeddingGenerator embeddingGenerator,
            ISearchIndexer searchIndexer,
            ILogger<WikipediaDataIngestionFunction> logger)
        {
            _articleSource = articleSource;
            _textChunker = textChunker;
            _embeddingGenerator = embeddingGenerator;
            _searchIndexer = searchIndexer;
            _logger = logger;
        }
        
        /// <summary>
        /// Timer-triggered function that processes Wikipedia articles and indexes them in Azure AI Search
        /// </summary>
        /// <param name="context">Function execution context</param>
        [Function("ProcessWikipediaArticles")]
        public async Task ProcessWikipediaArticlesAsync(
            [TimerTrigger("0 0 0 * * 0")] // Weekly on Sunday at midnight
            FunctionContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting Wikipedia data ingestion process at {Time}", DateTime.UtcNow);
            
            try
            {
                // 1. Fetch articles from the Hugging Face API
                _logger.LogInformation("Fetching articles from Hugging Face (limit: {Limit}, offset: {Offset})", _limit, _offset);
                var articles = await _articleSource.GetArticlesAsync(_limit, _offset);
                _logger.LogInformation("Retrieved {Count} articles", articles.Count());
                
                // 2. Process and chunk articles
                _logger.LogInformation("Chunking articles");
                var allChunks = new List<ArticleChunk>();
                
                foreach (var article in articles)
                {
                    _logger.LogInformation("Processing article: {Title} (ID: {Id})", article.Title, article.Id);
                    var chunks = _textChunker.ChunkArticle(article).ToList();
                    _logger.LogInformation("Article {Id} generated {Count} chunks", article.Id, chunks.Count);
                    allChunks.AddRange(chunks);
                }
                
                _logger.LogInformation("Total chunks generated: {Count}", allChunks.Count);
                
                if (!allChunks.Any())
                {
                    _logger.LogWarning("No chunks generated. Process will exit.");
                    return;
                }
                
                // 3. Generate embeddings for chunks
                _logger.LogInformation("Generating embeddings for {Count} chunks", allChunks.Count);
                var embeddings = await _embeddingGenerator.GenerateEmbeddingsAsync(allChunks);
                _logger.LogInformation("Generated {Count} embeddings", embeddings.Count);
                
                // 4. Create or update the search index
                _logger.LogInformation("Ensuring search index {IndexName} exists", _indexName);
                await _searchIndexer.CreateIndexIfNotExistsAsync(_indexName);
                
                // 5. Upload chunks with embeddings to the search index
                _logger.LogInformation("Uploading chunks and embeddings to search index");
                await _searchIndexer.UploadDocumentsAsync(_indexName, allChunks, embeddings);
                
                stopwatch.Stop();
                _logger.LogInformation(
                    "Completed Wikipedia data ingestion process. Processed {ArticleCount} articles and {ChunkCount} chunks in {ElapsedTime} seconds",
                    articles.Count(),
                    allChunks.Count,
                    stopwatch.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Wikipedia data ingestion process");
                throw;
            }
        }
        
        /// <summary>
        /// HTTP-triggered function for manually triggering the Wikipedia data ingestion process
        /// </summary>
        /// <param name="req">HTTP request</param>
        /// <returns>HTTP response</returns>
        [Function("ManualProcessWikipediaArticles")]
        public async Task<HttpResponseData> ManualProcessWikipediaArticlesAsync(
            [HttpTrigger(AuthorizationLevel.Admin, "post", Route = "process-wikipedia")]
            HttpRequestData req)
        {
            _logger.LogInformation("Manual trigger received for Wikipedia data ingestion");
            
            try
            {
                await ProcessWikipediaArticlesAsync(req.FunctionContext);
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteStringAsync("Wikipedia data ingestion completed successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during manual Wikipedia data ingestion process");
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error: {ex.Message}");
                return response;
            }
        }
    }
} 