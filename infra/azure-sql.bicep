// ============================================================
// azure-sql.bicep
// Deploys: Azure SQL Server (Entra ID-only auth) + Northwind
//          database + firewall rule for Azure services.
//          Grants the managed identity the
//          ##MS_DatabaseManager## server-level role.
// ============================================================

@description('Azure region for all resources.')
param location string = 'uksouth'

@description('Object ID (GUID) of the Entra ID user/group to be the SQL Entra admin.')
param adminObjectId string

@description('UPN (e.g. user@tenant.onmicrosoft.com) of the Entra ID SQL admin.')
param adminLogin string

@description('Principal ID of the user-assigned managed identity that needs DB access.')
param managedIdentityPrincipalId string

// ── Derived names (all lowercase) ───────────────────────────
var sqlServerName = 'sql-${uniqueString(resourceGroup().id)}'
var databaseName  = 'northwind'

// ── SQL Server ───────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name:     sqlServerName
  location: location
  properties: {
    // Entra ID-only authentication — no SQL password required
    administrators: {
      administratorType:         'ActiveDirectory'
      principalType:             'User'
      login:                     adminLogin
      sid:                       adminObjectId
      tenantId:                  subscription().tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// ── Entra-ID-only auth policy (child resource) ───────────────
resource aadOnlyAuth 'Microsoft.Sql/servers/azureADOnlyAuthentications@2021-11-01' = {
  parent: sqlServer
  name:   'Default'
  properties: {
    azureADOnlyAuthentication: true
  }
}

// ── Firewall: allow Azure-internal services ──────────────────
resource firewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name:   'AllowAllAzureIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress:   '0.0.0.0'
  }
}

// ── Northwind database (Basic tier) ─────────────────────────
resource northwindDb 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent:   sqlServer
  name:     databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation:   'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB (Basic tier max)
  }
}

// ── Grant managed identity SQL DB Contributor on the server ──
// Azure SQL supports only ONE Active Directory administrator per
// server (already set inline on the sqlServer resource above).
// To give the managed identity the equivalent of the T-SQL
// ##MS_DatabaseManager## role via ARM/Bicep we assign the
// built-in "SQL DB Contributor" RBAC role at the server scope.
// This lets the identity create and manage databases without
// requiring a second AD administrator entry.
//
// Built-in role GUID: 9b7fa17d-e63e-47b0-bb0a-15c516ac86ec
var sqlDbContributorRoleId = '9b7fa17d-e63e-47b0-bb0a-15c516ac86ec'

resource managedIdentitySqlContrib 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name:  guid(sqlServer.id, managedIdentityPrincipalId, sqlDbContributorRoleId)
  scope: sqlServer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sqlDbContributorRoleId)
    principalId:      managedIdentityPrincipalId
    principalType:    'ServicePrincipal'
  }
}

// ── Outputs ──────────────────────────────────────────────────
output sqlServerFqdn  string = sqlServer.properties.fullyQualifiedDomainName
output sqlServerName  string = sqlServer.name
output databaseName   string = northwindDb.name
