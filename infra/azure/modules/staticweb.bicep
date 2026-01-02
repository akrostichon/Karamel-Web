@description('Deploy a Static Web App')
param name string
param location string = resourceGroup().location

resource staticSite 'Microsoft.Web/staticSites@2025-03-01' = {
  name: name
  location: location
  properties: {
    // `sku` is not a permitted property for StaticSite in this API version
  }
}

output staticSiteId string = staticSite.id
output staticSiteName string = staticSite.name
