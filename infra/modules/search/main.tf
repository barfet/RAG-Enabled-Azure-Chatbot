// Azure Cognitive Search Module

resource "azurerm_search_service" "search" {
  name                = "${var.prefix}-search"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  replica_count       = 1
  partition_count     = 1
  tags                = var.tags

  # Enable semantic search (optional, only available in certain regions and SKUs)
  semantic_search_sku = "standard"

  # Public network access can be disabled for security
  public_network_access_enabled = true
} 