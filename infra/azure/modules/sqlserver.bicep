@description('Deploy an Azure SQL Server and a serverless database')
param serverName string
param dbName string
param location string = resourceGroup().location
param administratorLogin string
param administratorPassword string

resource sqlServer 'Microsoft.Sql/servers@2022-02-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    minimalTlsVersion: '1.2'
  }
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
  }
}

// Serverless database configuration uses sku and computeModel settings
resource sqlDb 'Microsoft.Sql/servers/databases@2021-02-01-preview' = {
  name: '${sqlServer.name}/${dbName}'
  location: location
  properties: {
    sku: {
      name: 'GP_S_Gen5_1'
      tier: 'GeneralPurpose'
    }
    zoneRedundant: false
  }
}

output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDb.name
output sqlServerFullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
