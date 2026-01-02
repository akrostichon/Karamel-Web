@description('Deploy an App Service Plan and Web App')
param name string
param planName string
param location string = resourceGroup().location

resource plan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: planName
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

resource webApp 'Microsoft.Web/sites@2025-03-01' = {
  name: name
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
}

output appServicePlanId string = plan.id
output webAppName string = webApp.name
output webAppId string = webApp.id
