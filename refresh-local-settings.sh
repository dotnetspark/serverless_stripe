#!/bin/bash
# Refresh STRIPE_API_KEY in local.settings.json from Azure Key Vault
set -e


# Use KEY_VAULT_NAME variable, defaulting to kv-serverless-stripe
KEY_VAULT_NAME=${KEY_VAULT_NAME:-"kv-serverless-stripe"}
SECRET_NAME=${STRIPE_SECRET_NAME:-"apiKey-Stripe"}

# Ensure Azure CLI is logged in
if ! az account show > /dev/null 2>&1; then
  echo "Azure CLI not logged in. Running az login..."
  az login
else
  echo "Azure CLI already logged in."
fi


# Get the secret value from Azure Key Vault
STRIPE_API_KEY=$(az keyvault secret show --vault-name "$KEY_VAULT_NAME" --name "$SECRET_NAME" --query value -o tsv)

if [ -z "$STRIPE_API_KEY" ]; then
  echo "Failed to fetch Stripe API key from Key Vault."
  exit 1
fi

# Update local.settings.json
jq ".Values.STRIPE_API_KEY = \"$STRIPE_API_KEY\"" azure-function/local.settings.json > azure-function/local.settings.json.tmp && mv azure-function/local.settings.json.tmp azure-function/local.settings.json

echo "local.settings.json updated with latest Stripe API key from Key Vault."
