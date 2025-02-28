// main.tf - Azure RAG Chatbot Infrastructure

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.80.0"
    }
  }
  backend "azurerm" {
    resource_group_name  = "terraform-state-rg"
    storage_account_name = "ragchatbottfstate"
    container_name       = "tfstate"
    key                  = "rag-chatbot.terraform.tfstate"
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy       = false
      purge_soft_deleted_keys_on_destroy = false
    }
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }
  skip_provider_registration = true
}

# Get current client configuration from the AzureRM provider
data "azurerm_client_config" "current" {}

resource "azurerm_resource_group" "rg" {
  name     = "${var.prefix}-rg"
  location = var.location
  tags     = var.tags
}

# Azure OpenAI Service
module "openai" {
  source              = "./modules/openai"
  prefix              = var.prefix
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  tags                = var.tags
}

# Azure Cognitive Search
module "search" {
  source              = "./modules/search"
  prefix              = var.prefix
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  tags                = var.tags
}

# Azure Cosmos DB
module "cosmosdb" {
  source              = "./modules/cosmosdb"
  prefix              = var.prefix
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  tags                = var.tags
}

# Azure Functions
module "functions" {
  source                = "./modules/functions"
  prefix                = var.prefix
  resource_group_name   = azurerm_resource_group.rg.name
  location              = var.location
  tags                  = var.tags
  openai_endpoint       = module.openai.endpoint
  openai_key            = module.openai.key
  search_endpoint       = module.search.endpoint
  search_key            = module.search.admin_key
  cosmosdb_endpoint     = module.cosmosdb.endpoint
  cosmosdb_key          = module.cosmosdb.primary_key
  cosmosdb_database     = module.cosmosdb.database_name
  cosmosdb_container    = module.cosmosdb.container_name
}

# Azure Key Vault for storing secrets
module "keyvault" {
  source              = "./modules/keyvault"
  prefix              = var.prefix
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  tags                = var.tags
  object_id           = data.azurerm_client_config.current.object_id
  tenant_id           = data.azurerm_client_config.current.tenant_id
  cosmosdb_key        = module.cosmosdb.primary_key
  openai_key          = module.openai.key
  search_key          = module.search.admin_key
}