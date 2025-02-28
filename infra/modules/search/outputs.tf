// Search module outputs

output "id" {
  value = azurerm_search_service.search.id
}

output "name" {
  value = azurerm_search_service.search.name
}

output "endpoint" {
  value = "https://${azurerm_search_service.search.name}.search.windows.net"
}

output "admin_key" {
  value     = azurerm_search_service.search.primary_key
  sensitive = true
}

output "query_key" {
  value     = azurerm_search_service.search.query_keys[0].key
  sensitive = true
} 