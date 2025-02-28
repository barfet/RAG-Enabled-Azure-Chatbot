// Functions module outputs

output "function_app_id" {
  value = azurerm_windows_function_app.function_app.id
}

output "function_app_name" {
  value = azurerm_windows_function_app.function_app.name
}

output "function_app_default_hostname" {
  value = azurerm_windows_function_app.function_app.default_hostname
}

output "function_app_principal_id" {
  value = azurerm_windows_function_app.function_app.identity[0].principal_id
}

output "app_insights_instrumentation_key" {
  value     = azurerm_application_insights.function_insights.instrumentation_key
  sensitive = true
}

output "app_insights_connection_string" {
  value     = azurerm_application_insights.function_insights.connection_string
  sensitive = true
} 