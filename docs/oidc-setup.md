# Configure OIDC Workload Identity Federation for GitHub Actions -> Azure

This document describes how to configure workload identity federation (OIDC) so GitHub Actions can authenticate to Azure without storing long-lived secrets.

Prerequisites
- You need permission to create or update an Azure AD App Registration and assign RBAC roles in the target subscription or resource group.
- `az` CLI and `gh` CLI are helpful but not required.

Overview
1. Create or reuse an Azure AD app registration (you may have created a service principal earlier with `az ad sp create-for-rbac`).
2. Add a Federated Identity Credential on the app registration that allows GitHub Actions from this repository/workflow to request tokens.
3. Configure the GitHub Actions workflow to call `azure/login@v2` with the app's `client-id` and tenant/subscription values.

Option A — Reuse existing service principal/app registration
If you already ran:

```
az ad sp create-for-rbac --name serverless-stripe --role contributor --scopes /subscriptions/<SUBSCRIPTION_ID> --sdk-auth
```

This created an app registration and service principal. Locate the app in the Azure Portal: Azure Active Directory -> App registrations -> search for "serverless-stripe".

Option B — Create a new App Registration (recommended if you haven't created one yet)
1. In Azure Portal, go to Azure Active Directory -> App registrations -> New registration.
2. Name: `serverless-stripe-github` (or similar).
3. Supported account types: Single tenant (or as appropriate).
4. Register.

Grant the app a minimal RBAC role scoped to the resource group or subscription you need:

```bash
# replace placeholders
az role assignment create --assignee <appId> --role "Contributor" --scope /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RG>
```

Add Federated Identity Credential (Portal)
1. Open the App registration in the portal.
2. Select "Certificates & secrets" -> "Federated credentials" -> "Add credential".
3. Fill in:
   - Name: `github-actions-ci` (or similar)
   - Issuer: `https://token.actions.githubusercontent.com`
   - Subject: `repo:<owner>/<repo>:environment:production` OR `repo:<owner>/<repo>:ref:refs/heads/main` (pick appropriate restriction)
   - Audience: `api://AzureADTokenExchange` (default for GitHub)
4. Save.

Add Federated Identity Credential (using Microsoft Graph)
- Use the Microsoft Graph API to add the cred if you prefer automation. See Microsoft docs for the JSON schema and required permissions.

Automation — get the App object id and add a federated credential (az / az rest)

If you prefer to automate the federated credential creation, use the `az` CLI to obtain the App's object id (the `id` field in Azure AD) and then call Microsoft Graph via `az rest` to POST the credential. Do not commit or paste real secret values into the repository — use placeholders or environment variables instead.

Example (replace placeholders and run from an account with appropriate Azure AD permissions):

```bash
# replace with your App (client) id
CLIENT_ID="<YOUR_CLIENT_ID>"

# get the App registration's object id
APP_OBJECT_ID=$(az ad app show --id "$CLIENT_ID" --query id -o tsv)
echo "App object id: $APP_OBJECT_ID"

# create a federated credential scoped to the main branch
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/applications/$APP_OBJECT_ID/federatedIdentityCredentials" \
  --headers "Content-Type=application/json" \
  --body '{
    "name": "github-actions-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<owner>/<repo>:ref:refs/heads/main",
    "description": "Allow GitHub Actions from main branch",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# the command will return the created credential JSON on success
```

Notes:
- The account running these commands needs rights to read and modify the App Registration (Azure AD application). If you see permission errors, run as a tenant administrator or use an account with the necessary Graph permissions.
- Use the `subject` value that matches how you want to restrict tokens (branch, environment, or workflow). See GitHub docs for the `subject` format.
- These commands are safe to keep in docs with placeholders; avoid inserting live IDs or secrets into the repository.

Configure GitHub repository secrets
- Store the following values as repo secrets (these are not secret except for subscription id if you prefer):
  - `AZURE_CLIENT_ID` — the App (client) ID from App registration
  - `AZURE_TENANT_ID` — your Azure AD tenant id
  - `AZURE_SUBSCRIPTION_ID` — subscription id (scoped to where you granted RBAC)

Workflow changes
- Update workflow step to use `azure/login@v2` like this:

```yaml
- name: Azure Login (OIDC)
  uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

Notes and security
- The `client-id` and `tenant-id` are not sensitive on their own; storing them as plaintext variables is acceptable, but many teams keep them as secrets for consistency.
- Restrict the federated credential `subject` to the exact repository and workflow branch or environment to avoid token misuse.
- Prefer using a separate app registration for CI with minimal RBAC scope.

Cleaning up a previously created Service Principal
- If you previously created a service principal and no longer want to use it, you can delete it. However, if you added a federated credential to that app registration, you can continue using the same app registration for OIDC.
- To delete the SP (if you created a temporary one):

```bash
az ad sp delete --id http://serverless-stripe
# or by object id
az ad sp delete --id <object-id>
```

If you are unsure, keep the SP until OIDC is working; there's no immediate need to delete it. Once OIDC is verified and your workflows run as expected, you can revoke/delete the SP to reduce credentials.

Troubleshooting
- If login fails with missing values, double check the app has a federated credential matching the `subject` used by your workflow.
- Check Azure AD sign-in logs and GitHub action logs for token exchange failures.

---
- A federated credential scoped to the `main` branch was added to the App Registration. The credential and app identifiers are redacted here — check the App Registration or Azure AD audit logs if you need the full details.

- Repository secrets for `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` should be set by a repo administrator. Do not publish these values in the repository; use the `gh` CLI or the GitHub UI to add them as Actions secrets.

Commands to set the repository secrets locally (run from a machine or environment with `gh` authenticated and repo admin access):

```bash
gh secret set AZURE_CLIENT_ID --repo <owner>/<repo> --body "<YOUR_CLIENT_ID>"
gh secret set AZURE_TENANT_ID --repo <owner>/<repo> --body "<YOUR_TENANT_ID>"
gh secret set AZURE_SUBSCRIPTION_ID --repo <owner>/<repo> --body "<YOUR_SUBSCRIPTION_ID>"
```

Next steps

- Push a commit to `main` (or wait for your next normal deployment) to trigger the workflow. The OIDC login step in the workflow should succeed for runs on `main` once the secrets are present and the federated credential matches the `subject` used by your workflow.
- After verifying the workflow, consider revoking any older federated credentials scoped to feature branches and optionally removing the temporary service principal credentials you created earlier (only if they're no longer needed).
