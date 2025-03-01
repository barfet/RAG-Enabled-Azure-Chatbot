# Wikipedia Data Ingestion Pipeline for RAG Chatbot

This Azure Function application implements a data ingestion pipeline for Wikipedia articles that:

1. Loads a subset of Wikipedia data from the Hugging Face dataset
2. Processes and chunks the Wikipedia articles into appropriate segments
3. Generates embeddings for these chunks using Azure OpenAI
4. Creates and populates an Azure AI Search index with the processed data and embeddings

## Architecture

The pipeline consists of the following components:

- **WikipediaService**: Fetches articles from the Hugging Face Wikipedia dataset
- **TextProcessingService**: Chunks articles into semantically meaningful segments
- **EmbeddingService**: Generates embeddings for text chunks using Azure OpenAI
- **SearchIndexService**: Creates and manages the Azure AI Search index
- **StorageService**: Stores raw articles in Azure Blob Storage

The Azure Function provides two endpoints:
- A timer-triggered function that runs on a schedule (`WikipediaDataIngestionScheduled`)
- An HTTP-triggered function for manual execution (`WikipediaDataIngestionManual`)

## Prerequisites

- Azure subscription
- .NET 7.0 SDK or later
- Azure Function Core Tools v4 or later
- The following Azure resources:
  - Azure OpenAI service with an embedding model deployed
  - Azure AI Search service
  - Azure Storage account
  - Azure Function App

## Configuration

The application uses the following configuration settings:

```json
{
  "OpenAI__Endpoint": "https://your-openai-service.openai.azure.com/",
  "OpenAI__Key": "your-openai-key",
  "OpenAI__EmbeddingsModelDeployment": "text-embedding-ada-002",
  
  "Search__Endpoint": "https://your-search-service.search.windows.net",
  "Search__Key": "your-search-key",
  "Search__IndexName": "wikipedia-index",
  
  "Storage__ConnectionString": "your-storage-connection-string",
  "Storage__ContainerName": "wikipedia-data",
  
  "Wikipedia__MaxArticlesToProcess": "1000",
  "Wikipedia__ChunkSize": "400",
  "Wikipedia__ChunkOverlap": "100"
}
```

## Local Development

1. Clone the repository
2. Navigate to the `backend/WikipediaIngestion/src` directory
3. Create a `local.settings.json` file with the configuration settings above
4. Run the function locally with the Azure Functions Core Tools:

```bash
func start
```

## Deployment

Deploy the function to Azure using the Azure Functions Core Tools:

```bash
func azure functionapp publish <your-function-app-name>
```

Alternatively, use Azure DevOps or GitHub Actions for CI/CD deployment.

## Usage

### Scheduled Execution

The function is configured to run automatically at midnight UTC daily. You can modify the CRON expression in the `TimerTrigger` attribute to change the schedule.

### Manual Execution

To trigger the ingestion process manually, send an HTTP request to the function endpoint:

```
GET https://your-function-app.azurewebsites.net/api/WikipediaDataIngestionManual?code=your-function-key
```

You can specify the number of articles to process using the `count` query parameter:

```
GET https://your-function-app.azurewebsites.net/api/WikipediaDataIngestionManual?code=your-function-key&count=500
```

## Monitoring

The application uses Application Insights for logging and monitoring. You can view logs and metrics in the Azure Portal under your Function App's Application Insights resource.

## Customization

### Chunking Strategy

The default chunking strategy attempts to preserve semantic boundaries by:
1. First splitting by sections (e.g., "Introduction", "History", etc.)
2. If sections are too large, splitting further by paragraphs
3. If paragraphs are too large, splitting by sentence boundaries

You can modify the chunking logic in the `TextProcessingService` class.

### Embedding Model

The pipeline uses the `text-embedding-ada-002` model by default. You can configure a different embedding model by changing the `OpenAI__EmbeddingsModelDeployment` setting.

### Search Index Schema

The Azure AI Search index schema is defined in the `AzureSearchIndexService` class. You can modify it to include additional fields or change the vector search configuration.

## Running Tests

The project includes comprehensive unit and integration tests to ensure the reliability of the Wikipedia Data Ingestion Function.

### Test Structure

Tests are organized by component:

- `TextProcessingServiceTests.cs`: Tests for the text chunking algorithm
- `WikipediaServiceTests.cs`: Tests for Wikipedia API interaction
- `AzureOpenAIEmbeddingServiceTests.cs`: Tests for embedding generation
- `AzureSearchIndexServiceTests.cs`: Tests for search index operations
- `AzureBlobStorageServiceTests.cs`: Tests for blob storage operations
- `WikipediaDataIngestionFunctionTests.cs`: Integration tests for the entire pipeline
- `ConfigurationTests.cs`: Tests for configuration handling

### Running the Tests

#### Using the Test Scripts

For convenience, we provide scripts to run all tests:

**Windows/PowerShell:**
```powershell
# From the tests directory
.\run-tests.ps1

# To run specific tests with a filter
.\run-tests.ps1 "FullyQualifiedName~AzureOpenAIEmbeddingServiceTests"
```

**Linux/macOS:**
```bash
# From the tests directory
./run-tests.sh

# To run specific tests with a filter
./run-tests.sh "FullyQualifiedName~AzureOpenAIEmbeddingServiceTests"

# Make the script executable if needed
chmod +x run-tests.sh
```

#### Using the .NET CLI directly

You can also run tests using the .NET CLI:

```bash
# Run all tests
dotnet test

# Run specific tests
dotnet test --filter "FullyQualifiedName~TextProcessingServiceTests"

# Generate code coverage report
dotnet test --collect:"XPlat Code Coverage"
```

### Coverage Reports

The test scripts include support for generating code coverage reports using the `reportgenerator` tool.

To install the required tool:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

After running the tests with the provided scripts, a coverage report will be generated in the `CoverageReport` directory.

## Troubleshooting

Common issues and their solutions:

1. **Function timeout**: Increase the function timeout value in `host.json`
2. **Memory constraints**: Consider chunking the ingestion process or using a higher memory tier
3. **Rate limiting**: Implement retry logic with exponential backoff (already included for Azure OpenAI)
4. **API errors**: Check the function logs for detailed error messages 