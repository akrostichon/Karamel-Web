@description('Deploy an App Service Plan and Web App')
param name string
param planName string
param location string = resourceGroup().location
@description('SKU name for the App Service Plan (e.g., F1, B1, S1)')
param planSkuName string = 'F1'
@description('SKU tier for the App Service Plan (e.g., Free, Basic, Standard)')
param planSkuTier string = 'Free'
@description('Plan capacity')
param planSkuCapacity int = 1

resource plan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: planName
  location: location
  sku: {
    name: planSkuName
    tier: planSkuTier
    capacity: planSkuCapacity
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2025-03-01' = {
  name: name
  location: location
  identity: {
    type: 'SystemAssigned'
  }
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
}

output appServicePlanId string = plan.id
output webAppName string = webApp.name
output webAppId string = webApp.id
output webAppPrincipalId string = webApp.identity.principalId
