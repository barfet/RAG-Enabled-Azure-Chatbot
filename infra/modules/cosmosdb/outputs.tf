// Cosmos DB module outputs

output "id" {
  value = azurerm_cosmosdb_account.db.id
}

output "name" {
  value = azurerm_cosmosdb_account.db.name
}

output "endpoint" {
  value = azurerm_cosmosdb_account.db.endpoint
}

output "primary_key" {
  value     = azurerm_cosmosdb_account.db.primary_key
  sensitive = true
}

output "connection_strings" {
  value     = azurerm_cosmosdb_account.db.connection_strings
  sensitive = true
}

output "database_name" {
  value = azurerm_cosmosdb_sql_database.db.name
}

output "container_name" {
  value = azurerm_cosmosdb_sql_container.sessions.name
}

output "logs_container_name" {
  value = azurerm_cosmosdb_sql_container.logs.name
} 