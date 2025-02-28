// variables.tf - Input variables for the RAG chatbot infrastructure

variable "prefix" {
  description = "The prefix to use for all resources in this deployment"
  type        = string
  default     = "ragchatbot"
}

variable "location" {
  description = "The Azure region where all resources will be created"
  type        = string
  default     = "westus"
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default = {
    environment = "development"
    project     = "rag-chatbot"
  }
}

variable "openai_model_deployment" {
  description = "The OpenAI model to deploy"
  type        = map(object({
    model_name    = string
    model_version = string
    scale_type    = string
  }))
  default = {
    "gpt-35-turbo" = {
      model_name    = "gpt-35-turbo"
      model_version = "0613"
      scale_type    = "Standard"
    },
    "text-embedding-ada-002" = {
      model_name    = "text-embedding-ada-002"
      model_version = "2"
      scale_type    = "Standard"
    }
  }
}

variable "cosmosdb_throughput" {
  description = "The throughput for the Cosmos DB container"
  type        = number
  default     = 400
}

variable "search_sku" {
  description = "The SKU for the Azure Cognitive Search service"
  type        = string
  default     = "standard"
}

variable "function_app_sku" {
  description = "The SKU for the Azure Functions App Service Plan"
  type        = object({
    tier = string
    size = string
  })
  default = {
    tier = "Standard"
    size = "S1"
  }
} 