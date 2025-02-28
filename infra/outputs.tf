// outputs.tf - Outputs from the infrastructure deployment

output "resource_group_name" {
  value = azurerm_resource_group.rg.name
}

output "openai_endpoint" {
  value     = module.openai.endpoint
  sensitive = false
}

output "search_endpoint" {
  value     = module.search.endpoint
  sensitive = false
}

output "cosmosdb_endpoint" {
  value     = module.cosmosdb.endpoint
  sensitive = false
}

output "function_app_name" {
  value = module.functions.function_app_name
}

output "function_app_default_hostname" {
  value = module.functions.function_app_default_hostname
}

output "keyvault_name" {
  value = module.keyvault.key_vault_name
}

output "deployment_instructions" {
  value = <<-EOT
    Deployment completed successfully!
    
    To use these resources:
    1. Access Key Vault '${module.keyvault.key_vault_name}' to get the secrets for the services
    2. Deploy your Azure Functions code to '${module.functions.function_app_name}'
    3. Configure your frontend to connect to the Function App endpoint: https://${module.functions.function_app_default_hostname}
    
    The infrastructure has been provisioned, but you'll need to:
    - Deploy your function app code
    - Upload and index your documents to the search service
    - Deploy any frontend applications
  EOT
} 