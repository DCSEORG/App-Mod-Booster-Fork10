// ============================================================
// genai.bicep
// Deploys: Azure OpenAI (swedencentral) + Azure AI Search
//          (uksouth), and wires RBAC for the supplied managed
//          identity.
// NOTE: OpenAI MUST be deployed to swedencentral regardless of
//       the resource group region.
// ============================================================

@description('Azure region for AI Search. OpenAI is always swedencentral.')
param location string = 'uksouth'

@description('Principal ID of the user-assigned managed identity.')
param managedIdentityPrincipalId string

// ── Fixed region for OpenAI ──────────────────────────────────
var openAILocation = 'swedencentral'

// ── Derived names (all lowercase) ───────────────────────────
var openAIName  = 'oai-${uniqueString(resourceGroup().id)}'
var searchName  = 'srch-${uniqueString(resourceGroup().id)}'

// ── Role Definition IDs (built-in) ──────────────────────────
// Cognitive Services OpenAI User
var openAIUserRoleId              = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
// Search Index Data Contributor
var searchIndexDataContribRoleId  = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
// Search Service Contributor
var searchServiceContribRoleId    = '7ca78c08-252a-4471-8644-bb5ff32d4ba0'

// ── Azure OpenAI ─────────────────────────────────────────────
resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name:     openAIName
  location: openAILocation
  kind:     'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    publicNetworkAccess:  'Enabled'
    customSubDomainName:  openAIName
    disableLocalAuth:     false
  }
}

// ── GPT-4o model deployment ──────────────────────────────────
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name:   'gpt-4o'
  sku: {
    name:     'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format:  'OpenAI'
      name:    'gpt-4o'
      version: '2024-05-13'
    }
    raiPolicyName: 'Microsoft.Default'
  }
}

// ── Azure AI Search ──────────────────────────────────────────
resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name:     searchName
  location: location
  sku: {
    name: 'standard'  // S0 — Bicep type system requires lowercase for Search SKU names
  }
  properties: {
    replicaCount:    1
    partitionCount:  1
    hostingMode:     'default'
    publicNetworkAccess: 'enabled'
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
  }
}

// ── RBAC: Cognitive Services OpenAI User → managed identity ──
resource openAIUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name:  guid(openAI.id, managedIdentityPrincipalId, openAIUserRoleId)
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', openAIUserRoleId)
    principalId:      managedIdentityPrincipalId
    principalType:    'ServicePrincipal'
  }
}

// ── RBAC: Search Index Data Contributor → managed identity ───
resource searchIndexDataContribRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name:  guid(aiSearch.id, managedIdentityPrincipalId, searchIndexDataContribRoleId)
  scope: aiSearch
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContribRoleId)
    principalId:      managedIdentityPrincipalId
    principalType:    'ServicePrincipal'
  }
}

// ── RBAC: Search Service Contributor → managed identity ──────
resource searchServiceContribRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name:  guid(aiSearch.id, managedIdentityPrincipalId, searchServiceContribRoleId)
  scope: aiSearch
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchServiceContribRoleId)
    principalId:      managedIdentityPrincipalId
    principalType:    'ServicePrincipal'
  }
}

// ── Outputs ──────────────────────────────────────────────────
output openAIEndpoint   string = openAI.properties.endpoint
output openAIModelName  string = gpt4oDeployment.name
output openAIName       string = openAI.name
output searchEndpoint   string = 'https://${aiSearch.name}.search.windows.net'
output searchName       string = aiSearch.name
