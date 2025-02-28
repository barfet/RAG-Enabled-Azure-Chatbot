// Functions module variables

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

variable "openai_endpoint" {
  description = "The endpoint URL for the Azure OpenAI service"
  type        = string
}

variable "openai_key" {
  description = "The API key for the Azure OpenAI service"
  type        = string
  sensitive   = true
}

variable "search_endpoint" {
  description = "The endpoint URL for the Azure Cognitive Search service"
  type        = string
}

variable "search_key" {
  description = "The admin API key for the Azure Cognitive Search service"
  type        = string
  sensitive   = true
}

variable "cosmosdb_endpoint" {
  description = "The endpoint URL for the Azure Cosmos DB account"
  type        = string
}

variable "cosmosdb_key" {
  description = "The primary key for the Azure Cosmos DB account"
  type        = string
  sensitive   = true
}

variable "cosmosdb_database" {
  description = "The name of the Azure Cosmos DB database"
  type        = string
}

variable "cosmosdb_container" {
  description = "The name of the Azure Cosmos DB container for chat sessions"
  type        = string
} 