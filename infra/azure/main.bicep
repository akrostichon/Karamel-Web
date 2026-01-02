
@description('Name prefix for deployed resources (e.g. karamel-dev)')
param namePrefix string
param location string = resourceGroup().location
param sqlAdminUser string
@secure()
param sqlAdminPassword string

var kvName = '${namePrefix}-kv'
var sqlServerName = '${namePrefix}-sqlsrv'
var sqlDbName = '${namePrefix}-sqldb'
var appServicePlanName = '${namePrefix}-plan'
var webAppName = '${namePrefix}-api'
var staticSiteName = '${namePrefix}-static'
var appInsightsName = '${namePrefix}-ai'

module kvModule 'modules/keyvault.bicep' = {
  name: 'keyvaultModule'
  params: {
    name: kvName
    location: location
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
  }
}

module webModule 'modules/webapp.bicep' = {
  name: 'webModule'
  params: {
    name: webAppName
    planName: appServicePlanName
    location: location
  }
}

module staticModule 'modules/staticweb.bicep' = {
  name: 'staticModule'
  params: {
    name: staticSiteName
    location: location
  }
}

output keyVaultName string = kvModule.outputs.keyVaultName
output sqlServer string = sqlModule.outputs.sqlServerName
output sqlDatabase string = sqlModule.outputs.sqlDatabaseName
output webAppName string = webModule.outputs.webAppName
output staticSiteName string = staticModule.outputs.staticSiteName
output appInsights string = aiModule.outputs.appInsightsName
