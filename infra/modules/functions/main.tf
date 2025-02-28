// Azure Functions Module

# Storage account for Azure Functions
resource "azurerm_storage_account" "function_storage" {
  name                     = "${var.prefix}fnstore"
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  tags                     = var.tags
}

# App Service Plan
resource "azurerm_service_plan" "function_plan" {
  name                = "${var.prefix}-function-plan"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Windows"
  sku_name            = "Y1"
  tags                = var.tags
}

# Application Insights for monitoring
resource "azurerm_application_insights" "function_insights" {
  name                = "${var.prefix}-function-insights"
  location            = var.location
  resource_group_name = var.resource_group_name
  application_type    = "web"
  tags                = var.tags
}

# Function App
resource "azurerm_windows_function_app" "function_app" {
  name                       = "${var.prefix}-function-app"
  resource_group_name        = var.resource_group_name
  location                   = var.location
  storage_account_name       = azurerm_storage_account.function_storage.name
  storage_account_access_key = azurerm_storage_account.function_storage.primary_access_key
  service_plan_id            = azurerm_service_plan.function_plan.id
  
  # Enable managed identity for Key Vault access
  identity {
    type = "SystemAssigned"
  }
  
  site_config {
    application_stack {
      dotnet_version = "v6.0"
    }
    cors {
      allowed_origins = ["*"] # Restrict this in production
    }
  }

  app_settings = {
    # General settings
    "APPINSIGHTS_INSTRUMENTATIONKEY"        = azurerm_application_insights.function_insights.instrumentation_key
    "FUNCTIONS_WORKER_RUNTIME"              = "dotnet"
    "WEBSITE_CONTENTAZUREFILECONNECTIONSTRING" = azurerm_storage_account.function_storage.primary_connection_string
    "WEBSITE_CONTENTSHARE"                 = "${var.prefix}-function-app"
    
    # OpenAI settings
    "OpenAI__Endpoint"                     = var.openai_endpoint
    "OpenAI__Key"                          = var.openai_key
    "OpenAI__ChatModelDeployment"          = "gpt-4o-mini"
    "OpenAI__EmbeddingsModelDeployment"    = "text-embedding-ada-002"
    
    # Azure Cognitive Search settings
    "Search__Endpoint"                     = var.search_endpoint
    "Search__Key"                          = var.search_key
    "Search__IndexName"                    = "ragindex"
    
    # Cosmos DB settings
    "CosmosDB__Endpoint"                   = var.cosmosdb_endpoint
    "CosmosDB__Key"                        = var.cosmosdb_key
    "CosmosDB__DatabaseName"               = var.cosmosdb_database
    "CosmosDB__ChatSessionsContainer"      = "chatsessions"
    "CosmosDB__LogsContainer"              = "logs"
  }

  tags = var.tags
} 