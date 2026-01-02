@description('Deploy an Azure SQL Server and a serverless database')
param serverName string
param dbName string
param location string = resourceGroup().location
param administratorLogin string
@secure()
param administratorPassword string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    minimalTlsVersion: '1.2'
  }
}

// Serverless database configuration uses sku and computeModel settings
resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01' = {
  name: dbName
  parent: sqlServer
  location: location
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
  }
  properties: {
    zoneRedundant: false
  }
}

output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDb.name
output sqlServerFullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
