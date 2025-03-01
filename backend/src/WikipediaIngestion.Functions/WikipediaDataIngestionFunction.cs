using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private int _offset; // Not readonly to avoid CA1805
        
        // Define logger message delegates for better performance
        private static readonly Action<ILogger, DateTime, Exception?> _startingIngestion =
            LoggerMessage.Define<DateTime>(LogLevel.Information, 
                new EventId(1, nameof(ProcessWikipediaArticlesAsync)), 
                "Starting Wikipedia data ingestion process at {Time}");
                
        private static readonly Action<ILogger, int, int, Exception?> _fetchingArticles =
            LoggerMessage.Define<int, int>(LogLevel.Information, 
                new EventId(2, nameof(ProcessWikipediaArticlesAsync)), 
                "Fetching articles from Hugging Face (limit: {Limit}, offset: {Offset})");
                
        private static readonly Action<ILogger, int, Exception?> _retrievedArticles =
            LoggerMessage.Define<int>(LogLevel.Information, 
                new EventId(3, nameof(ProcessWikipediaArticlesAsync)), 
                "Retrieved {Count} articles");
                
        private static readonly Action<ILogger, Exception?> _chunkingArticles =
            LoggerMessage.Define(LogLevel.Information, 
                new EventId(4, nameof(ProcessWikipediaArticlesAsync)), 
                "Chunking articles");
                
        private static readonly Action<ILogger, string, string, Exception?> _processingArticle =
            LoggerMessage.Define<string, string>(LogLevel.Information, 
                new EventId(5, nameof(ProcessWikipediaArticlesAsync)), 
                "Processing article: {Title} (ID: {Id})");
                
        private static readonly Action<ILogger, string, int, Exception?> _articleChunksGenerated =
            LoggerMessage.Define<string, int>(LogLevel.Information, 
                new EventId(6, nameof(ProcessWikipediaArticlesAsync)), 
                "Article {Id} generated {Count} chunks");
                
        private static readonly Action<ILogger, int, Exception?> _totalChunksGenerated =
            LoggerMessage.Define<int>(LogLevel.Information, 
                new EventId(7, nameof(ProcessWikipediaArticlesAsync)), 
                "Total chunks generated: {Count}");
                
        private static readonly Action<ILogger, Exception?> _noChunksWarning =
            LoggerMessage.Define(LogLevel.Warning, 
                new EventId(8, nameof(ProcessWikipediaArticlesAsync)), 
                "No chunks generated. Process will exit.");
                
        private static readonly Action<ILogger, int, Exception?> _generatingEmbeddings =
            LoggerMessage.Define<int>(LogLevel.Information, 
                new EventId(9, nameof(ProcessWikipediaArticlesAsync)), 
                "Generating embeddings for {Count} chunks");
                
        private static readonly Action<ILogger, int, Exception?> _generatedEmbeddings =
            LoggerMessage.Define<int>(LogLevel.Information, 
                new EventId(10, nameof(ProcessWikipediaArticlesAsync)), 
                "Generated {Count} embeddings");
                
        private static readonly Action<ILogger, string, Exception?> _ensuringIndexExists =
            LoggerMessage.Define<string>(LogLevel.Information, 
                new EventId(11, nameof(ProcessWikipediaArticlesAsync)), 
                "Ensuring search index {IndexName} exists");
                
        private static readonly Action<ILogger, Exception?> _uploadingChunks =
            LoggerMessage.Define(LogLevel.Information, 
                new EventId(12, nameof(ProcessWikipediaArticlesAsync)), 
                "Uploading chunks and embeddings to search index");
                
        private static readonly Action<ILogger, int, int, double, Exception?> _completedIngestion =
            LoggerMessage.Define<int, int, double>(LogLevel.Information, 
                new EventId(13, nameof(ProcessWikipediaArticlesAsync)), 
                "Completed Wikipedia data ingestion process. Processed {ArticleCount} articles and {ChunkCount} chunks in {ElapsedTime} seconds");
                
        private static readonly Action<ILogger, Exception?> _manualTriggerReceived =
            LoggerMessage.Define(LogLevel.Information, 
                new EventId(14, nameof(ManualProcessWikipediaArticlesAsync)), 
                "Manual trigger received for Wikipedia data ingestion");
                
        private static readonly Action<ILogger, Exception> _errorDuringIngestion =
            LoggerMessage.Define(LogLevel.Error, 
                new EventId(15, nameof(ProcessWikipediaArticlesAsync)), 
                "Error occurred during Wikipedia data ingestion process");
                
        private static readonly Action<ILogger, Exception> _errorDuringManualIngestion =
            LoggerMessage.Define(LogLevel.Error, 
                new EventId(16, nameof(ManualProcessWikipediaArticlesAsync)), 
                "Error occurred during manual Wikipedia data ingestion process");

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
            ArgumentNullException.ThrowIfNull(articleSource);
            ArgumentNullException.ThrowIfNull(textChunker);
            ArgumentNullException.ThrowIfNull(embeddingGenerator);
            ArgumentNullException.ThrowIfNull(searchIndexer);
            ArgumentNullException.ThrowIfNull(logger);
            
            _articleSource = articleSource;
            _textChunker = textChunker;
            _embeddingGenerator = embeddingGenerator;
            _searchIndexer = searchIndexer;
            _logger = logger;
            _offset = 0; // Initialize here to avoid CS0649
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
            ArgumentNullException.ThrowIfNull(context);
            
            var stopwatch = Stopwatch.StartNew();
            _startingIngestion(_logger, DateTime.UtcNow, null);
            
            try
            {
                // 1. Fetch articles from the Hugging Face API
                _fetchingArticles(_logger, _limit, _offset, null);
                var articles = await _articleSource.GetArticlesAsync(_limit, _offset).ConfigureAwait(false);
                _retrievedArticles(_logger, articles.Count(), null);
                
                // 2. Process and chunk articles
                _chunkingArticles(_logger, null);
                var allChunks = new List<ArticleChunk>();
                
                foreach (var article in articles)
                {
                    _processingArticle(_logger, article.Title, article.Id, null);
                    var chunks = _textChunker.ChunkArticle(article).ToList();
                    _articleChunksGenerated(_logger, article.Id, chunks.Count, null);
                    allChunks.AddRange(chunks);
                }
                
                _totalChunksGenerated(_logger, allChunks.Count, null);
                
                if (allChunks.Count == 0)
                {
                    _noChunksWarning(_logger, null);
                    return;
                }
                
                // 3. Generate embeddings for chunks
                _generatingEmbeddings(_logger, allChunks.Count, null);
                var embeddings = await _embeddingGenerator.GenerateEmbeddingsAsync(allChunks).ConfigureAwait(false);
                _generatedEmbeddings(_logger, embeddings.Count, null);
                
                // 4. Create or update the search index
                _ensuringIndexExists(_logger, _indexName, null);
                await _searchIndexer.CreateIndexIfNotExistsAsync(_indexName).ConfigureAwait(false);
                
                // 5. Upload chunks with embeddings to the search index
                _uploadingChunks(_logger, null);
                await _searchIndexer.UploadDocumentsAsync(_indexName, allChunks, embeddings).ConfigureAwait(false);
                
                stopwatch.Stop();
                _completedIngestion(
                    _logger,
                    articles.Count(),
                    allChunks.Count,
                    stopwatch.Elapsed.TotalSeconds,
                    null);
            }
            catch (Exception ex)
            {
                _errorDuringIngestion(_logger, ex);
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
            ArgumentNullException.ThrowIfNull(req);
            
            _manualTriggerReceived(_logger, null);
            
            try
            {
                await ProcessWikipediaArticlesAsync(req.FunctionContext).ConfigureAwait(false);
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteStringAsync("Wikipedia data ingestion completed successfully").ConfigureAwait(false);
                return response;
            }
            catch (ArgumentException ex)
            {
                _errorDuringManualIngestion(_logger, ex);
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await response.WriteStringAsync($"Invalid argument: {ex.Message}").ConfigureAwait(false);
                return response;
            }
            catch (TimeoutException ex)
            {
                _errorDuringManualIngestion(_logger, ex);
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.RequestTimeout);
                await response.WriteStringAsync($"Operation timed out: {ex.Message}").ConfigureAwait(false);
                return response;
            }
            catch (HttpRequestException ex)
            {
                _errorDuringManualIngestion(_logger, ex);
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.ServiceUnavailable);
                await response.WriteStringAsync($"Service unavailable: {ex.Message}").ConfigureAwait(false);
                return response;
            }
            catch (System.Net.WebException ex)
            {
                _errorDuringManualIngestion(_logger, ex);
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.BadGateway);
                await response.WriteStringAsync($"Network error: {ex.Message}").ConfigureAwait(false);
                return response;
            }
            catch (IOException ex)
            {
                _errorDuringManualIngestion(_logger, ex);
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"I/O error: {ex.Message}").ConfigureAwait(false);
                return response;
            }
            catch (UnauthorizedAccessException ex)
            {
                _errorDuringManualIngestion(_logger, ex);
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.Forbidden);
                await response.WriteStringAsync($"Access denied: {ex.Message}").ConfigureAwait(false);
                return response;
            }
            catch (AggregateException ex)
            {
                _errorDuringManualIngestion(_logger, ex);
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Multiple errors occurred: {ex.Message}").ConfigureAwait(false);
                return response;
            }
        }
    }
} 