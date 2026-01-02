@description('Deploy a Static Web App')
param name string
param location string = resourceGroup().location

resource staticSite 'Microsoft.Web/staticSites@2025-03-01' = {
  name: name
  location: location
  properties: {
    sku: {
      name: 'Free'
    }
  }
}

output staticSiteId string = staticSite.id
output staticSiteName string = staticSite.name
