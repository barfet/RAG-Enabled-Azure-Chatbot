// Key Vault module variables

variable "prefix" {
  description = "The prefix to use for all resources in this deployment"
  type        = string
}

variable "location" {
  description = "The Azure region to deploy the resources"
  type        = string
}

variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
}

variable "openai_key" {
  description = "The API key for the Azure OpenAI service"
  type        = string
  sensitive   = true
}

variable "search_key" {
  description = "The admin API key for the Azure Cognitive Search service"
  type        = string
  sensitive   = true
}

variable "cosmosdb_key" {
  description = "The primary key for the Azure Cosmos DB account"
  type        = string
  sensitive   = true
}

variable "object_id" {
  description = "The object ID of the current user or service principal"
  type        = string
}

variable "tenant_id" {
  description = "The tenant ID for the Azure Active Directory"
  type        = string
} 