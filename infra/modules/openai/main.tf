// Azure OpenAI Service Terraform Module

resource "azurerm_cognitive_account" "openai" {
  name                = "${var.prefix}-openai"
  location            = var.location
  resource_group_name = var.resource_group_name
  kind                = "OpenAI"
  sku_name            = "S0"
  tags                = var.tags

  lifecycle {
    prevent_destroy = false
  }
}

# Deploy GPT-3.5 Turbo model
resource "azurerm_cognitive_deployment" "gpt_35_turbo" {
  name                 = "gpt-35-turbo"
  cognitive_account_id = azurerm_cognitive_account.openai.id
  
  model {
    format  = "OpenAI"
    name    = "gpt-35-turbo"
    version = "1106"
  }
  
  scale {
    type     = "Standard"
    capacity = 1
  }
}

# Deploy text-embedding-ada-002 model for embeddings
resource "azurerm_cognitive_deployment" "embedding" {
  name                 = "text-embedding-ada-002"
  cognitive_account_id = azurerm_cognitive_account.openai.id
  
  model {
    format  = "OpenAI"
    name    = "text-embedding-ada-002"
    version = "2"
  }
  
  scale {
    type     = "Standard"
    capacity = 1
  }
} 