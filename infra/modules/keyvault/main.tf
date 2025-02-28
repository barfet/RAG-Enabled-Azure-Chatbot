// Azure Key Vault Module

# Create a Key Vault to store all the sensitive information
resource "azurerm_key_vault" "kv" {
  name                        = "${var.prefix}-kv"
  location                    = var.location
  resource_group_name         = var.resource_group_name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days  = 7
  purge_protection_enabled    = false
  sku_name                    = "standard"

  # Configure access policies as needed
  # For example, grant the current user full access
  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = data.azurerm_client_config.current.object_id

    key_permissions = [
      "Backup", "Create", "Decrypt", "Delete", "Encrypt", "Get", "Import", "List", "Purge", 
      "Recover", "Restore", "Sign", "UnwrapKey", "Update", "Verify", "WrapKey"
    ]

    secret_permissions = [
      "Backup", "Delete", "Get", "List", "Purge", "Recover", "Restore", "Set"
    ]

    certificate_permissions = [
      "Backup", "Create", "Delete", "DeleteIssuers", "Get", "GetIssuers", "Import", "List", 
      "ListIssuers", "ManageContacts", "ManageIssuers", "Purge", "Recover", "Restore", "SetIssuers", "Update"
    ]
  }

  tags = var.tags
}

# Get current Azure client configuration
data "azurerm_client_config" "current" {}

# Store the OpenAI key as a secret
resource "azurerm_key_vault_secret" "openai_key" {
  name         = "openai-key"
  value        = var.openai_key
  key_vault_id = azurerm_key_vault.kv.id
}

# Store the Search key as a secret
resource "azurerm_key_vault_secret" "search_key" {
  name         = "search-key"
  value        = var.search_key
  key_vault_id = azurerm_key_vault.kv.id
}

# Store the Cosmos DB key as a secret
resource "azurerm_key_vault_secret" "cosmosdb_key" {
  name         = "cosmosdb-key"
  value        = var.cosmosdb_key
  key_vault_id = azurerm_key_vault.kv.id
} 