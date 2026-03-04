// ============================================================
// app-service.bicep
// Deploys: App Service Plan, App Service (Web App),
//          and a User-Assigned Managed Identity
// ============================================================

@description('Azure region for all resources.')
param location string = 'uksouth'

@description('Base name used to derive resource names. Defaults to a unique string scoped to the resource group.')
param appName string = 'app-${uniqueString(resourceGroup().id)}'

// ── Derived names (all lowercase) ───────────────────────────
var planName     = 'asp-${toLower(appName)}'
var webAppName   = 'wa-${toLower(appName)}'
var identityName = 'mid-appmodassist-${uniqueString(resourceGroup().id)}'

// ── User-Assigned Managed Identity ──────────────────────────
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name:     identityName
  location: location
}

// ── App Service Plan (Standard S1) ──────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name:     planName
  location: location
  sku: {
    name:     'S1'
    tier:     'Standard'
    size:     'S1'
    capacity: 1
  }
  kind: 'app'
  properties: {
    reserved: false
  }
}

// ── App Service (Web App) ────────────────────────────────────
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name:     webAppName
  location: location
  kind:     'app'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly:    true
    siteConfig: {
      minTlsVersion:    '1.2'
      ftpsState:        'Disabled'
      http20Enabled:    true
      // Bind the managed identity as the identity used for
      // key-vault and other SDK calls from within the app.
      keyVaultReferenceIdentity: managedIdentity.id
    }
  }
}

// ── Outputs ──────────────────────────────────────────────────
output appServiceName              string = appService.name
output appServiceUrl               string = 'https://${appService.properties.defaultHostName}'
output managedIdentityClientId     string = managedIdentity.properties.clientId
output managedIdentityPrincipalId  string = managedIdentity.properties.principalId
output managedIdentityId           string = managedIdentity.id
