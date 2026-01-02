@description('Deploy a Key Vault')
param name string
param location string = resourceGroup().location
@description('Enable purge protection (irreversible once true). Disabled because you cannot delete the Key Vault if enabled.')
param enablePurgeProtection bool = false
@description('Whether to create a new Key Vault. If false, module will not create resources and will return the provided name as output')
param createVault bool = true

var kvResourceName = name

resource kv 'Microsoft.KeyVault/vaults@2025-05-01' = if (createVault) {
  name: kvResourceName
  location: location
  properties: union({
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
  }, enablePurgeProtection ? { enablePurgeProtection: true } : {})
}

output keyVaultName string = kvResourceName
output keyVaultId string = createVault ? kv.id : subscriptionResourceId('Microsoft.KeyVault/vaults', kvResourceName)
