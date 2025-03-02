# User Story: Wikipedia Data Ingestion Pipeline for RAG Chatbot

## Description
As a developer of the RAG-enabled Azure Chatbot, I want to create a data ingestion pipeline implemented as a Python Azure Function that will:
1. Load a subset of Wikipedia data from the Hugging Face dataset
2. Process and chunk the Wikipedia articles into appropriate segments
3. Generate embeddings for these chunks using Azure OpenAI
4. Create and populate an Azure AI Search index with the processed data and embeddings

This will establish the knowledge base foundation that our chatbot will use to retrieve relevant information when responding to user queries.

## Technical Details

### Data Source
- **Dataset**: Wikimedia/Wikipedia from Hugging Face (https://huggingface.co/datasets/wikimedia/wikipedia)
- **Subset Selection**: We'll initially focus on a manageable subset (e.g., English Wikipedia articles on a specific topic or category, or a random sample of ~1000 articles) to demonstrate the pipeline functionality while keeping processing time reasonable
- **Python Package**: Use the `datasets` library from Hugging Face to directly access and download the dataset, rather than working via API

### Azure Function Implementation
- **Language/Framework**: Python with Azure Functions v4
- **Trigger Type**: Timer-triggered function for scheduled updates and HTTP-triggered function for manual execution
- **Function Name**: `WikipediaDataIngestionFunction`

### Processing Pipeline Components

1. **Data Extraction**:
   - Use the Hugging Face `datasets` Python library to download the selected Wikipedia subset
   - Alternative approach: Use a pre-downloaded dump if the Hugging Face library has limitations
   - Store raw articles temporarily in Azure Blob Storage (optional, for checkpointing)
   - Leverage the `azure-storage-blob` Python package for Azure Blob Storage operations

2. **Data Processing**:
   - Implement article chunking with semantic boundaries (paragraphs or sections, ~300-500 tokens per chunk)
   - Extract metadata for each chunk:
     - Title of the Wikipedia article
     - Section headers/hierarchy
     - Last update timestamp
     - URL/reference to the original article
     - Category information (if available)

3. **Embedding Generation**:
   - Connect to Azure OpenAI Service using the `openai` or `azure-openai` Python package
   - Use the text-embedding-ada-002 model (or newer embedding model if available)
   - Generate embeddings for each text chunk
   - Implement batching to efficiently process multiple chunks (to handle rate limits)

4. **Search Index Management**:
   - Create Azure AI Search index with appropriate schema if it doesn't exist
   - Define fields for content, metadata, and vector embeddings
   - Configure the index for hybrid search (keyword + vector)
   - Upload processed chunks with their embeddings
   - Use the `azure-search-documents` Python package for Azure AI Search operations

5. **Monitoring and Logging**:
   - Implement Application Insights telemetry using `opencensus-ext-azure` Python package
   - Log progress, errors, and completion status
   - Track key metrics (processing time, chunk counts, etc.)

### Azure Search Index Schema

```json
{
  "name": "wikipedia-index",
  "fields": [
    { "name": "id", "type": "Edm.String", "key": true, "filterable": true },
    { "name": "title", "type": "Edm.String", "searchable": true, "retrievable": true, "filterable": true, "sortable": true },
    { "name": "content", "type": "Edm.String", "searchable": true, "retrievable": true },
    { "name": "section", "type": "Edm.String", "searchable": true, "retrievable": true, "filterable": true },
    { "name": "url", "type": "Edm.String", "retrievable": true },
    { "name": "lastUpdated", "type": "Edm.DateTimeOffset", "retrievable": true, "filterable": true, "sortable": true },
    { "name": "category", "type": "Collection(Edm.String)", "retrievable": true, "filterable": true, "facetable": true },
    { 
      "name": "contentVector", 
      "type": "Collection(Edm.Single)", 
      "dimensions": 1536, 
      "vectorSearchConfiguration": "my-vector-config" 
    }
  ],
  "vectorSearch": {
    "algorithmConfigurations": [
      {
        "name": "my-vector-config",
        "kind": "hnsw",
        "parameters": {
          "m": 4,
          "efConstruction": 400,
          "efSearch": 500,
          "metric": "cosine"
        }
      }
    ]
  },
  "semantic": {
    "configurations": [
      {
        "name": "my-semantic-config",
        "prioritizedFields": {
          "titleField": { "fieldName": "title" },
          "contentFields": [{ "fieldName": "content" }],
          "keywordsFields": [{ "fieldName": "category" }]
        }
      }
    ]
  }
}
```

## Implementation Steps

1. **Project Setup**:
   - Create a new Azure Functions project using Python
   - Install required Python packages:
     - `azure-functions`
     - `azure-identity`
     - `azure-storage-blob`
     - `azure-search-documents`
     - `azure-openai`
     - `datasets` (Hugging Face)
     - `transformers` (for any additional NLP needs)
     - `opencensus-ext-azure` (for Application Insights)
     - `pytest` and `pytest-asyncio` (for test-driven development)

2. **Configuration**:
   - Set up local.settings.json with required connection strings and API keys:
     - Azure OpenAI endpoint and key
     - Azure AI Search endpoint and admin key
     - Azure Blob Storage connection string (if using)
     - Application Insights instrumentation key

3. **Function Implementation**:
   - Implement modular components for each pipeline stage
   - Create helper modules for:
     - Wikipedia data fetching and parsing
     - Text chunking algorithms
     - Embedding generation with retry logic
     - Search index management

4. **Processing Logic**:
   - Implement chunking strategies that preserve context
   - Build batch processing with error handling and resume capability
   - Add telemetry for monitoring progress and diagnosing issues
   - Implement rate limiting to handle Azure OpenAI service constraints

5. **Test-Driven Development**:
   - Write unit tests for each component before implementation
   - Create test fixtures with sample Wikipedia articles
   - Implement mock services for Azure dependencies to enable offline testing
   - Establish CI pipeline for automated testing
   - Ensure code coverage meets project standards (minimum 80%)

6. **Deployment**:
   - Deploy the function to Azure
   - Configure appropriate timeout settings (since processing may take time)
   - Set up managed identity for secure access to Azure services

## Acceptance Criteria

1. The Azure Function successfully downloads a subset of Wikipedia articles from Hugging Face using the Python `datasets` library
2. Articles are properly chunked with appropriate semantic boundaries
3. Embeddings are correctly generated for each chunk using Azure OpenAI
4. An Azure AI Search index is created with the specified schema
5. Processed chunks and their embeddings are successfully indexed in Azure AI Search
6. The function includes proper error handling and logging
7. The function can be triggered both on a schedule and manually via HTTP
8. Documentation is provided for the pipeline, including configuration requirements
9. Performance metrics (processing time, chunks processed) are captured
10. Comprehensive test suite is implemented following test-driven development practices
11. All tests pass with at least 80% code coverage

## Test Cases

1. **Unit Tests**:
   - Test chunking algorithm with various article lengths
   - Test metadata extraction from Wikipedia format
   - Test embedding generation with sample text
   - Test search index schema creation and validation
   - Test each component in isolation with appropriate mocks

2. **Integration Tests**:
   - Test end-to-end pipeline with a small sample of articles
   - Verify correct generation of embeddings via Azure OpenAI
   - Verify correct indexing in Azure AI Search
   - Test the full pipeline with mock services

3. **Manual Tests**:
   - Trigger the function manually and verify successful execution
   - Query the search index to ensure data is retrievable
   - Test vector search on sample queries to validate embeddings quality
   - Verify that metadata is correctly preserved and searchable

4. **Performance Tests**:
   - Measure processing time for various batch sizes
   - Evaluate memory usage during processing
   - Test concurrent execution scenarios (if applicable)

## Additional Considerations

1. **Error Handling**:
   - Implement retry logic for transient errors with Azure OpenAI
   - Add checkpointing to resume processing if interrupted
   - Log detailed error information for troubleshooting

2. **Security**:
   - Use Azure Managed Identity for accessing services when possible
   - Store API keys in Azure Key Vault
   - Follow security guidelines from the project documentation

3. **Scalability**:
   - Evaluate memory consumption for large batches
   - Consider chunking the Wikipedia dataset processing itself into multiple function executions
   - Monitor Azure OpenAI rate limits and implement appropriate throttling

4. **Future Enhancements**:
   - Add capability to incrementally update the index with new/changed articles
   - Implement more sophisticated chunking strategies (e.g., sliding windows with overlap)
   - Add content filtering to remove inappropriate or irrelevant content
   - Create a self-contained Python package for reusability across different data sources

This user story provides a comprehensive starting point for implementing the Wikipedia data ingestion pipeline for our RAG-enabled Azure Chatbot using Python. The implementation will follow test-driven development practices and leverage Python packages to directly interact with Hugging Face datasets and Azure services. Once completed, we'll have a solid foundation for our knowledge base that the chatbot will use to retrieve relevant information when answering user queries.
