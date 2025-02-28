// Azure Cosmos DB Module

resource "azurerm_cosmosdb_account" "db" {
  name                = "${var.prefix}-cosmos"
  location            = var.location
  resource_group_name = var.resource_group_name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB" # SQL API

  consistency_policy {
    consistency_level       = "Session"
    max_interval_in_seconds = 5
    max_staleness_prefix    = 100
  }

  geo_location {
    location          = var.location
    failover_priority = 0
  }

  # Network settings (can be locked down further for production)
  public_network_access_enabled = true
  
  # Enable capabilities like Synapse Link or Azure AD auth if needed
  # capabilities {
  #   name = "EnableServerless"
  # }

  tags = var.tags
}

resource "azurerm_cosmosdb_sql_database" "db" {
  name                = "ragchatbot"
  resource_group_name = var.resource_group_name
  account_name        = azurerm_cosmosdb_account.db.name
  throughput          = var.throughput
}

# Container for chat sessions
resource "azurerm_cosmosdb_sql_container" "sessions" {
  name                = "chatsessions"
  resource_group_name = var.resource_group_name
  account_name        = azurerm_cosmosdb_account.db.name
  database_name       = azurerm_cosmosdb_sql_database.db.name
  partition_key_path  = "/sessionId"
  
  # Optional: Set TTL for automatic cleanup of old sessions
  default_ttl = 2592000 # 30 days in seconds
  
  # Enable indexing for frequently queried fields
  indexing_policy {
    indexing_mode = "consistent"
    
    included_path {
      path = "/*"
    }
    
    excluded_path {
      path = "/\"_etag\"/?"
    }
  }
}

# Container for logs
resource "azurerm_cosmosdb_sql_container" "logs" {
  name                = "logs"
  resource_group_name = var.resource_group_name
  account_name        = azurerm_cosmosdb_account.db.name
  database_name       = azurerm_cosmosdb_sql_database.db.name
  partition_key_path  = "/timestamp"
  
  # Optional: Set TTL for automatic cleanup of old logs
  default_ttl = 7776000 # 90 days in seconds
  
  indexing_policy {
    indexing_mode = "consistent"
    
    included_path {
      path = "/*"
    }
    
    excluded_path {
      path = "/\"_etag\"/?"
    }
  }
} 