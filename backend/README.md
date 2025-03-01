# Wikipedia Data Ingestion Pipeline

This project implements a pipeline for ingesting Wikipedia articles into Azure AI Search for use in a RAG (Retrieval-Augmented Generation) chatbot.

## Architecture

The pipeline consists of the following components:

1. **Article Source** - Fetches Wikipedia articles from the Hugging Face API
2. **Text Chunker** - Splits articles into smaller chunks for better retrieval
3. **Embedding Generator** - Generates vector embeddings for each chunk using Azure OpenAI
4. **Search Indexer** - Creates and updates an Azure AI Search index with the chunks and embeddings

The pipeline is orchestrated by an Azure Function that can be triggered on a schedule or manually.

## Project Structure

- **WikipediaIngestion.Core** - Contains domain models and interfaces
- **WikipediaIngestion.Infrastructure** - Contains implementations of the interfaces
- **WikipediaIngestion.Functions** - Contains the Azure Function that orchestrates the pipeline
- **WikipediaIngestion.UnitTests** - Contains unit tests for the components

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- Azure subscription with:
  - Azure OpenAI service
  - Azure AI Search service
- Hugging Face API key

### Configuration

Update the `local.settings.json` file in the `WikipediaIngestion.Functions` project with your API keys and endpoints:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "HuggingFace:ApiKey": "your-huggingface-api-key",
    "AzureOpenAI:Endpoint": "https://your-openai-resource.openai.azure.com",
    "AzureOpenAI:DeploymentName": "your-embedding-deployment",
    "AzureOpenAI:ApiKey": "your-openai-api-key",
    "AzureOpenAI:ApiVersion": "2023-12-01-preview",
    "AzureSearch:Endpoint": "https://your-search-resource.search.windows.net",
    "AzureSearch:ApiKey": "your-search-admin-key"
  }
}
```

### Running the Function Locally

1. Install the Azure Functions Core Tools
2. Run `func start` in the `WikipediaIngestion.Functions` directory

### Deployment

Deploy the function to Azure using the Azure Functions extension for Visual Studio or the Azure CLI:

```bash
func azure functionapp publish <function-app-name>
```

## Testing

Run the unit tests with:

```bash
dotnet test tests/WikipediaIngestion.UnitTests/WikipediaIngestion.UnitTests.csproj
```

## Components

### HuggingFaceArticleSource

Fetches Wikipedia articles from the Hugging Face API. The API returns articles in JSON format with the following properties:

- `id` - The article ID
- `title` - The article title
- `text` - The article content
- `url` - The URL to the article on Wikipedia
- `timestamp` - The last update timestamp
- `categories` - A list of categories the article belongs to

### ParagraphTextChunker

Splits articles into smaller chunks based on paragraphs and section headers. Each chunk contains:

- `id` - A unique ID for the chunk
- `articleId` - The ID of the parent article
- `articleTitle` - The title of the parent article
- `content` - The chunk content
- `sectionTitle` - The title of the section the chunk belongs to
- `articleUrl` - The URL to the parent article on Wikipedia

### AzureOpenAIEmbeddingGenerator

Generates vector embeddings for each chunk using Azure OpenAI's embedding model. The embeddings are used for semantic search in Azure AI Search.

### AzureSearchIndexer

Creates and updates an Azure AI Search index with the chunks and embeddings. The index includes:

- Vector search configuration for semantic search
- Semantic configuration for hybrid search
- Fields for all chunk properties and the embedding vector

## License

This project is licensed under the MIT License - see the LICENSE file for details. 