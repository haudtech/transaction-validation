@description('Azure region for all resources.')
param location string

param keyVaultName string
param tags object = {}

@secure()
@description('Value stored as the Security:ApiKey secret; pass at deploy time, never hardcode.')
param apiKeySecretValue string

@secure()
@description('Redis connection string, computed by the caller from listKeys() and stored as a secret.')
param redisConnectionStringSecretValue string

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
  }
}

resource apiKeySecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'Security--ApiKey'
  properties: {
    value: apiKeySecretValue
  }
}

resource redisConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'Redis--ConnectionString'
  properties: {
    value: redisConnectionStringSecretValue
  }
}

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
