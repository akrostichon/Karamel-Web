
@description('Name prefix for deployed resources (e.g. karamel-dev)')
param namePrefix string
param location string = 'northeurope'
param sqlAdminUser string
@secure()
param sqlAdminPassword string
@description('If true, create a new Key Vault. Otherwise use an existing vault name provided by keyVaultName')
param createKeyVault bool = true
@description('Name of Key Vault to create or reuse')
param keyVaultName string = '${namePrefix}-kv'
@description('Location to create the Static Web App (some resource types are not available in all regions)')
param staticLocation string = 'westeurope'

@description('Create a private endpoint and related networking for the SQL Server')
param createSqlPrivateEndpoint bool = false
@description('VNet name for SQL private endpoint (optional)')
param sqlVnetName string = ''
@description('Subnet name inside the VNet for the SQL private endpoint (optional)')
param sqlSubnetName string = ''
@description('VNet CIDR (used when a VNet is created)')
param sqlVnetPrefix string = '10.1.0.0/16'
@description('Subnet CIDR (used when a VNet is created)')
param sqlSubnetPrefix string = '10.1.0.0/24'

var sqlServerName = '${namePrefix}-sqlsrv'
var sqlDbName = '${namePrefix}-sqldb'
var appServicePlanName = '${namePrefix}-plan'
var webAppName = '${namePrefix}-api'
var staticSiteName = '${namePrefix}-static'
var appInsightsName = '${namePrefix}-ai'
@description('App Service Plan SKU name')
param appServicePlanSkuName string = 'F1'
@description('App Service Plan SKU tier')
param appServicePlanSkuTier string = 'Free'
@description('App Service Plan capacity')
param appServicePlanSkuCapacity int = 1
@description('Location for App Service resources (App Service Plan & Web App)')
param appServiceLocation string = 'westeurope'

module kvModule 'modules/keyvault.bicep' = {
  name: 'keyvaultModule'
  params: {
    name: keyVaultName
    location: location
    createVault: createKeyVault
  }
}

module aiModule 'modules/appinsights.bicep' = {
  name: 'appInsightsModule'
  params: {
    name: appInsightsName
    location: location
  }
}

module sqlModule 'modules/sqlserver.bicep' = {
  name: 'sqlModule'
  params: {
    serverName: sqlServerName
    dbName: sqlDbName
    location: location
    administratorLogin: sqlAdminUser
    administratorPassword: sqlAdminPassword
    createPrivateEndpoint: createSqlPrivateEndpoint
    vnetName: empty(sqlVnetName) ? '${sqlServerName}-vnet' : sqlVnetName
    subnetName: empty(sqlSubnetName) ? 'sql-subnet' : sqlSubnetName
    vnetAddressPrefix: sqlVnetPrefix
    subnetPrefix: sqlSubnetPrefix
  }
}

module webModule 'modules/webapp.bicep' = {
  name: 'webModule'
  params: {
    name: webAppName
    planName: appServicePlanName
    location: appServiceLocation
    planSkuName: appServicePlanSkuName
    planSkuTier: appServicePlanSkuTier
    planSkuCapacity: appServicePlanSkuCapacity
  }
}

module staticModule 'modules/staticweb.bicep' = {
  name: 'staticModule'
  params: {
    name: staticSiteName
    location: staticLocation
  }
}

output keyVaultName string = kvModule.outputs.keyVaultName
output sqlServer string = sqlModule.outputs.sqlServerName
output sqlDatabase string = sqlModule.outputs.sqlDatabaseName
output webAppName string = webModule.outputs.webAppName
// Role assignment: grant Key Vault Secrets User to web app managed identity
// Built-in role definition ID for 'Key Vault Secrets User'
var keyVaultSecretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')

// Reference the Key Vault as an existing resource so we can use it as the scope (type: resource)
resource existingKeyVault 'Microsoft.KeyVault/vaults@2021-06-01' existing = {
  name: keyVaultName
}

resource webToKvRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (createKeyVault) {
  // Use only values that are known at deployment start for the role assignment name
  name: guid(subscription().id, resourceGroup().name, keyVaultName, webModule.name, 'kv-secrets-user')
  scope: existingKeyVault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleId
    principalId: webModule.outputs.webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output staticSiteName string = staticModule.outputs.staticSiteName
output appInsights string = aiModule.outputs.appInsightsName
