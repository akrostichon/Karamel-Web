@description('Name prefix for deployed resources (e.g. karamel-dev)')
param namePrefix string
param location string = resourceGroup().location
param sqlAdminUser string
param sqlAdminPassword string

var kvName = '${namePrefix}-kv'
var sqlServerName = '${namePrefix}-sqlsrv'
var sqlDbName = '${namePrefix}-sqldb'
var appServicePlanName = '${namePrefix}-plan'
var webAppName = '${namePrefix}-api'
var staticSiteName = '${namePrefix}-static'
var appInsightsName = '${namePrefix}-ai'

resource kv 'Microsoft.KeyVault/vaults@2022-11-01' = {
  name: kvName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enablePurgeProtection: false
    enableSoftDelete: true
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  properties: {
    Application_Type: 'web'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2022-02-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminUser
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2021-02-01-preview' = {
  name: '${sqlServer.name}/${sqlDbName}'
  location: location
  properties: {
    sku: {
      name: 'GP_S_Gen5_1'
      tier: 'GeneralPurpose'
    }
    zoneRedundant: false
  }
}

resource plan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: plan.id
    siteConfig: {
      appSettings: [
        {
          name: 'WEBSITES_ENABLE_WEBSOCKETS'
          value: '1'
        }
      ]
    }
  }
  dependsOn: [ plan ]
}

resource staticSite 'Microsoft.Web/staticSites@2022-03-01' = {
  name: staticSiteName
  location: location
  properties: {
    sku: {
      name: 'Free'
    }
  }
}

output keyVaultName string = kv.name
output sqlServer string = sqlServer.name
output sqlDatabase string = sqlDb.name
output webAppName string = webApp.name
output staticSiteName string = staticSite.name
output appInsights string = appInsights.name
