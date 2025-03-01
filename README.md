# RAG-Enabled Azure Chatbot

A Retrieval-Augmented Generation (RAG) chatbot built on Azure services, designed to provide accurate and contextually relevant responses by leveraging both pre-trained language models and a knowledge base of Wikipedia articles.

## Architecture

The solution consists of two main components:

1. **Backend Data Ingestion Pipeline** - Processes Wikipedia articles and indexes them in Azure AI Search
2. **Frontend Chat Interface** - Provides a user interface for interacting with the chatbot

### Backend Architecture

The backend pipeline includes:

- **Article Source** - Fetches Wikipedia articles from the Hugging Face API
- **Text Chunker** - Splits articles into smaller chunks for better retrieval
- **Embedding Generator** - Generates vector embeddings using Azure OpenAI
- **Search Indexer** - Creates and updates an Azure AI Search index

### Frontend Architecture

The frontend includes:

- **Chat Interface** - A web application for interacting with the chatbot
- **RAG Service** - Handles retrieval of relevant information and generation of responses

## Technologies Used

- **Azure OpenAI** - For generating embeddings and chat completions
- **Azure AI Search** - For storing and retrieving article chunks
- **Azure Functions** - For orchestrating the data ingestion pipeline
- **React** - For building the frontend chat interface
- **.NET 9.0** - For implementing the backend services

## Getting Started

### Prerequisites

- Azure subscription with:
  - Azure OpenAI service
  - Azure AI Search service
- Hugging Face API key
- .NET 9.0 SDK
- Node.js and npm

### Setup

1. Clone the repository
2. Set up the backend (see [backend/README.md](backend/README.md))
3. Set up the frontend (see [frontend/README.md](frontend/README.md))

## Development

### Backend Development

The backend is implemented as a .NET solution with the following projects:

- **WikipediaIngestion.Core** - Contains domain models and interfaces
- **WikipediaIngestion.Infrastructure** - Contains implementations of the interfaces
- **WikipediaIngestion.Functions** - Contains the Azure Function that orchestrates the pipeline
- **WikipediaIngestion.UnitTests** - Contains unit tests for the components

### Frontend Development

The frontend is implemented as a React application with the following features:

- Chat interface with message history
- Integration with Azure OpenAI for generating responses
- Integration with Azure AI Search for retrieving relevant information

## Deployment

### Backend Deployment

Deploy the Azure Function to Azure:

```bash
cd backend/src/WikipediaIngestion.Functions
func azure functionapp publish <function-app-name>
```

### Frontend Deployment

Deploy the frontend to Azure Static Web Apps or another hosting service.

## License

This project is licensed under the MIT License - see the LICENSE file for details.