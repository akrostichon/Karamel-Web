@description('Deploy an Azure SQL Server and a serverless database')
param serverName string
param dbName string
param location string = resourceGroup().location
param administratorLogin string
@secure()
param administratorPassword string

@description('Whether to create a private endpoint and VNet for the SQL server')
param createPrivateEndpoint bool = false
@description('Name of the VNet to create for the private endpoint (when enabled)')
param vnetName string = '${serverName}-vnet'
@description('Subnet name to create/use inside the VNet for private endpoint')
param subnetName string = 'sql-subnet'
@description('VNet address prefix')
param vnetAddressPrefix string = '10.1.0.0/16'
@description('Subnet address prefix')
param subnetPrefix string = '10.1.0.0/24'
@description('Private DNS zone for Azure SQL private endpoint')
param privateDnsZoneName string = 'privatelink.database.windows.net'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
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
    autoPauseDelay: 40 // minutes of inactivity before auto-pause
  }
}

// When private endpoint is requested, create a VNet + subnet and a private endpoint
resource vnet 'Microsoft.Network/virtualNetworks@2021-05-01' = if (createPrivateEndpoint) {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [ vnetAddressPrefix ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: subnetPrefix
        }
      }
    ]
  }
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (createPrivateEndpoint) {
  name: privateDnsZoneName
  location: 'global'
  properties: {}
}

resource vnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if (createPrivateEndpoint) {
  name: '${vnet.name}-link'
  parent: privateDnsZone
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2021-05-01' = if (createPrivateEndpoint) {
  name: '${serverName}-pe'
  location: location
  properties: {
    subnet: {
      id: resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, subnetName)
    }
    privateLinkServiceConnections: [
      {
        name: '${serverName}-pls'
        properties: {
          privateLinkServiceId: sqlServer.id
          groupIds: [ 'sqlServer' ]
        }
      }
    ]
  }
}

// Link private endpoint to the private DNS zone so name resolution works
resource peDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2021-05-01' = if (createPrivateEndpoint) {
  name: 'default'
  parent: privateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: privateDnsZone.id
        }
      }
    ]
  }
}

output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDb.name
output sqlServerFullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
output privateEndpointId string = createPrivateEndpoint ? privateEndpoint.id : ''
output privateDnsZoneId string = createPrivateEndpoint ? privateDnsZone.id : ''
