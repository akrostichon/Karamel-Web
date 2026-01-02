@description('Deploy a Key Vault')
param name string
param location string = resourceGroup().location
@description('Enable purge protection (irreversible once true)')
param enablePurgeProtection bool = true

resource kv 'Microsoft.KeyVault/vaults@2025-05-01' = {
  name: name
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enablePurgeProtection: enablePurgeProtection
    enableSoftDelete: true
  }
}

output keyVaultName string = kv.name
output keyVaultId string = kv.id
