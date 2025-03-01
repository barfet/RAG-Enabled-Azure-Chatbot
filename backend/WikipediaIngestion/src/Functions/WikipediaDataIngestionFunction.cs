using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WikipediaDataIngestionFunction.Services;

namespace WikipediaDataIngestionFunction.Functions
{
    public class WikipediaDataIngestionFunction
    {
        private readonly IWikipediaService _wikipediaService;
        private readonly ITextProcessingService _textProcessingService;
        private readonly IEmbeddingService _embeddingService;
        private readonly ISearchIndexService _searchIndexService;
        private readonly IStorageService _storageService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WikipediaDataIngestionFunction> _logger;
        
        // Configuration values
        private readonly int _maxArticlesToProcess;
        private readonly int _chunkSize;
        private readonly int _chunkOverlap;
        
        public WikipediaDataIngestionFunction(
            IWikipediaService wikipediaService,
            ITextProcessingService textProcessingService,
            IEmbeddingService embeddingService,
            ISearchIndexService searchIndexService,
            IStorageService storageService,
            IConfiguration configuration,
            ILogger<WikipediaDataIngestionFunction> logger)
        {
            _wikipediaService = wikipediaService;
            _textProcessingService = textProcessingService;
            _embeddingService = embeddingService;
            _searchIndexService = searchIndexService;
            _storageService = storageService;
            _configuration = configuration;
            _logger = logger;
            
            // Load configuration
            _maxArticlesToProcess = int.Parse(_configuration["Wikipedia__MaxArticlesToProcess"] ?? "1000");
            _chunkSize = int.Parse(_configuration["Wikipedia__ChunkSize"] ?? "400");
            _chunkOverlap = int.Parse(_configuration["Wikipedia__ChunkOverlap"] ?? "100");
        }
        
        // Timer-triggered function that runs on a schedule (e.g., once a day)
        [Function("WikipediaDataIngestionScheduled")]
        public async Task RunScheduled([TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo)
        {
            _logger.LogInformation("Wikipedia Data Ingestion function executed at: {Time}", DateTime.UtcNow);
            
            try
            {
                await ProcessWikipediaData(_maxArticlesToProcess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled Wikipedia data ingestion");
            }
        }
        
        // HTTP-triggered function for manual execution
        [Function("WikipediaDataIngestionManual")]
        public async Task<HttpResponseData> RunManual(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("Manual Wikipedia Data Ingestion function executed at: {Time}", DateTime.UtcNow);
            
            // Get the number of articles to process from the query string
            string countParam = req.Url.Query.Contains("count=") 
                ? req.Url.Query.Split("count=")[1].Split("&")[0] 
                : _maxArticlesToProcess.ToString();
            
            int articleCount = int.TryParse(countParam, out int count) ? count : _maxArticlesToProcess;
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            
            try
            {
                await response.WriteStringAsync($"Starting Wikipedia data ingestion process for {articleCount} articles...\n");
                await ProcessWikipediaData(articleCount);
                await response.WriteStringAsync($"Wikipedia data ingestion completed successfully at {DateTime.UtcNow}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manual Wikipedia data ingestion");
                response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error processing Wikipedia data: {ex.Message}");
            }
            
            return response;
        }
        
        private async Task ProcessWikipediaData(int articleCount)
        {
            _logger.LogInformation("Starting Wikipedia data ingestion for {Count} articles", articleCount);
            
            // Step 1: Create search index if it doesn't exist
            _logger.LogInformation("Step 1: Creating search index if it doesn't exist");
            await _searchIndexService.CreateIndexIfNotExistsAsync();
            
            // Step 2: Fetch Wikipedia articles
            _logger.LogInformation("Step 2: Fetching Wikipedia articles");
            var articles = await _wikipediaService.GetArticlesAsync(articleCount);
            _logger.LogInformation("Retrieved {Count} articles from Wikipedia", articles.Count);
            
            // Step 3: Save raw articles to blob storage for future reference
            _logger.LogInformation("Step 3: Saving raw articles to blob storage");
            foreach (var article in articles)
            {
                await _storageService.SaveRawArticleAsync(article);
            }
            
            // Step 4: Process and chunk articles
            _logger.LogInformation("Step 4: Processing and chunking articles");
            var allChunks = new List<TextChunk>();
            
            foreach (var article in articles)
            {
                var chunks = _textProcessingService.ChunkArticle(article, _chunkSize, _chunkOverlap);
                allChunks.AddRange(chunks);
            }
            
            _logger.LogInformation("Created {Count} chunks from {ArticleCount} articles", allChunks.Count, articles.Count);
            
            // Step 5: Generate embeddings for chunks
            _logger.LogInformation("Step 5: Generating embeddings for chunks");
            var chunksWithEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(allChunks);
            
            // Step 6: Index chunks in Azure AI Search
            _logger.LogInformation("Step 6: Indexing chunks in Azure AI Search");
            await _searchIndexService.IndexChunksAsync(chunksWithEmbeddings);
            
            _logger.LogInformation("Wikipedia data ingestion process completed successfully");
        }
    }
} 