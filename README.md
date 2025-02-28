# RAG-Enabled Azure Chatbot

A chatbot leveraging Retrieval-Augmented Generation (RAG) pattern to answer questions based on proprietary data, built on Azure services.

## Architecture

This project implements a RAG-enabled chatbot with the following components:

- **Frontend**: React-based chat interface for user interaction
- **Backend**: Azure Functions for API orchestration and business logic
- **Vector Store**: Azure Cognitive Search with vector search capabilities
- **LLM Integration**: Azure OpenAI Service for embeddings and chat completions
- **Persistence**: Azure Cosmos DB for conversation history storage

For detailed architecture information, see [docs/architecture.md](docs/architecture.md).

## Project Structure

```
project-root/
├── frontend/          # React frontend application (UI)
├── backend/           # Azure Functions backend (API logic)
├── infra/             # Infrastructure as code (Bicep)
├── docs/              # Documentation files
└── scripts/           # Utility scripts
```

## Getting Started

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed and logged in
- [Node.js](https://nodejs.org/) (v14 or later)
- Access to an Azure subscription with permissions to create resources
- Quota for Azure OpenAI services in your selected region

### Infrastructure Deployment

1. Deploy the Azure infrastructure:

```bash
# Navigate to the infrastructure directory
cd infra

# Deploy resources using Bicep
./deploy.sh
```

2. Index your documents:

```bash
# Install Node.js dependencies
npm install

# Index documents from a directory
npm run index -- --documents ./path/to/docs --category "Company Policies"
```

### Backend Deployment

After setting up the infrastructure:

```bash
# Navigate to the backend directory
cd backend

# Install dependencies
npm install

# Deploy to Azure Functions
func azure functionapp publish <function-app-name>
```

### Frontend Deployment

```bash
# Navigate to the frontend directory
cd frontend

# Install dependencies
npm install

# Build the frontend
npm run build

# Deploy to Azure Static Website
az storage blob upload-batch -d '$web' -s ./build --account-name <storage-account-name>
```

## Development

For local development:

1. Run the backend locally:
```bash
cd backend
npm install
func start
```

2. Run the frontend locally:
```bash
cd frontend
npm install
npm start
```

## Security Considerations

This project implements several security best practices:

- All API keys and secrets are stored in Azure Key Vault
- Azure Functions use Managed Identity to access other Azure services
- HTTPS is enforced for all communication
- Key rotation and secure credential management

For more details, see the security guidelines in the documentation.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.