// ============================================================
// main.bicep
// Orchestrates all infrastructure modules:
//   • app-service   – App Service Plan, Web App, Managed Identity
//   • azure-sql     – SQL Server (Entra-only) + Northwind DB
//   • genai         – Azure OpenAI + AI Search (conditional)
// ============================================================

@description('Primary Azure region for deployments.')
param location string = 'uksouth'

@description('Object ID (GUID) of the Entra ID user/group to be the SQL Entra admin.')
param adminObjectId string

@description('UPN (e.g. user@tenant.onmicrosoft.com) of the Entra ID SQL admin.')
param adminLogin string

@description('Set to true to deploy Azure OpenAI and AI Search resources.')
param deployGenAI bool = false

// ── App Service + Managed Identity ──────────────────────────
module appServiceModule 'app-service.bicep' = {
  name: 'deploy-app-service'
  params: {
    location: location
  }
}

// ── Azure SQL Server + Northwind Database ────────────────────
module sqlModule 'azure-sql.bicep' = {
  name: 'deploy-azure-sql'
  params: {
    location:                   location
    adminObjectId:              adminObjectId
    adminLogin:                 adminLogin
    managedIdentityPrincipalId: appServiceModule.outputs.managedIdentityPrincipalId
  }
}

// ── GenAI (Azure OpenAI + AI Search) ─────────────────────────
// Deployed only when deployGenAI == true.
// OpenAI is always deployed to swedencentral (enforced inside
// the genai module); AI Search follows `location`.
module genaiModule 'genai.bicep' = if (deployGenAI) {
  name: 'deploy-genai'
  params: {
    location:                   location
    managedIdentityPrincipalId: appServiceModule.outputs.managedIdentityPrincipalId
  }
}

// ── Outputs ──────────────────────────────────────────────────

// App Service
output appServiceUrl               string = appServiceModule.outputs.appServiceUrl
output appServiceName              string = appServiceModule.outputs.appServiceName

// Managed Identity
output managedIdentityClientId     string = appServiceModule.outputs.managedIdentityClientId
output managedIdentityPrincipalId  string = appServiceModule.outputs.managedIdentityPrincipalId

// SQL
output sqlServerFqdn               string = sqlModule.outputs.sqlServerFqdn
output databaseName                string = sqlModule.outputs.databaseName

// GenAI (null-safe — empty string when not deployed)
// Use null-conditional accessor (?.) so Bicep knows the module
// may be skipped and falls back to '' via the ?? operator.
output openAIEndpoint              string = genaiModule.?outputs.openAIEndpoint  ?? ''
output openAIModelName             string = genaiModule.?outputs.openAIModelName ?? ''
output openAIName                  string = genaiModule.?outputs.openAIName      ?? ''
output searchEndpoint              string = genaiModule.?outputs.searchEndpoint  ?? ''
output searchName                  string = genaiModule.?outputs.searchName      ?? ''
