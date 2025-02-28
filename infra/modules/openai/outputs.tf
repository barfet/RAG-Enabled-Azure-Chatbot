// OpenAI module outputs

output "id" {
  value = azurerm_cognitive_account.openai.id
}

output "name" {
  value = azurerm_cognitive_account.openai.name
}

output "endpoint" {
  value = azurerm_cognitive_account.openai.endpoint
}

output "key" {
  value     = azurerm_cognitive_account.openai.primary_access_key
  sensitive = true
}

output "gpt_deployment_id" {
  value = azurerm_cognitive_deployment.gpt-4o-mini.name
}

output "embedding_deployment_id" {
  value = azurerm_cognitive_deployment.embedding.name
} 