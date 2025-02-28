// Cosmos DB module variables

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

variable "throughput" {
  description = "The throughput for the Cosmos DB container"
  type        = number
  default     = 400
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
} 