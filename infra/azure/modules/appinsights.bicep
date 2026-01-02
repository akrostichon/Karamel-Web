@description('Deploy Application Insights resource')
param name string
param location string = resourceGroup().location

resource ai 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  properties: {
    Application_Type: 'web'
  }
  kind: 'web'
}

output appInsightsName string = ai.name
output appInsightsId string = ai.id
