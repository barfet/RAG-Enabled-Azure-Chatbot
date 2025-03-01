# Wikipedia Data Ingestion Pipeline Integration Tests

This project contains integration tests for the Wikipedia Data Ingestion Pipeline. These tests verify that the entire pipeline functions correctly with real external services.

## Prerequisites

To run these integration tests, you need:

1. An Azure OpenAI account with an embedding model deployment
2. An Azure AI Search instance
3. A Hugging Face API key

## Configuration

Before running the tests, you need to set up the configuration:

1. Create a copy of `appsettings.json` and name it `appsettings.Development.json`
2. Update the values in `appsettings.Development.json` with your actual API keys and endpoints:

```json
{
  "HuggingFaceApiKey": "YOUR_HUGGINGFACE_API_KEY",
  "AzureOpenAI": {
    "Endpoint": "https://your-openai-resource.openai.azure.com/",
    "DeploymentName": "your-embedding-deployment",
    "ApiKey": "YOUR_AZURE_OPENAI_API_KEY",
    "ApiVersion": "2023-05-15"
  },
  "AzureSearch": {
    "Endpoint": "https://your-search-service.search.windows.net",
    "ApiKey": "YOUR_AZURE_SEARCH_API_KEY"
  }
}
```

Alternatively, you can set these values as environment variables:

```
HuggingFaceApiKey=YOUR_HUGGINGFACE_API_KEY
AzureOpenAI__Endpoint=https://your-openai-resource.openai.azure.com/
AzureOpenAI__DeploymentName=your-embedding-deployment
AzureOpenAI__ApiKey=YOUR_AZURE_OPENAI_API_KEY
AzureOpenAI__ApiVersion=2023-05-15
AzureSearch__Endpoint=https://your-search-service.search.windows.net
AzureSearch__ApiKey=YOUR_AZURE_SEARCH_API_KEY
```

## Running the Tests

To run the integration tests:

```bash
cd backend
dotnet test tests/WikipediaIngestion.IntegrationTests
```

If you don't have all the API keys configured, some tests will be skipped automatically.

## Test Categories

The integration tests are organized into the following categories:

1. **Component Tests** - Test individual components with real services:
   - `ArticleSource_ShouldRetrieveArticles` - Tests the HuggingFaceArticleSource with the real API
   - `TextChunker_ShouldChunkArticle` - Tests the ParagraphTextChunker with realistic article content
   - `EmbeddingGenerator_ShouldGenerateEmbeddings` - Tests the AzureOpenAIEmbeddingGenerator with the real API
   - `SearchIndexer_ShouldCreateAndManageIndex` - Tests the AzureSearchIndexer with the real API

2. **Full Pipeline Tests** - Test the entire pipeline from end to end:
   - `FullPipeline_ShouldProcessArticlesEndToEnd` - Tests the manual execution of all pipeline steps
   - `ProcessWikipediaArticlesAsync_ShouldProcessAndIndexArticles` - Tests the Azure Function execution

## Test Customization

The integration tests use a test-specific index name to avoid interfering with production data. By default, the tests will process a small number of articles to minimize API costs and execution time.

You can modify these values in the test code if needed:

- In `WikipediaDataIngestionPipelineTests.cs`, change the `_testIndexName` value
- In `WikipediaDataIngestionFunctionTests.cs`, adjust the `limit` parameter in the `CustomizeForTesting` method call

## Cleanup

The tests automatically clean up any test indexes created during execution, both before and after test runs. If a test fails unexpectedly, you may need to manually delete the test indexes from your Azure AI Search instance. 