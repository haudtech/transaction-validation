targetScope = 'subscription'

@description('Azure region for all resources.')
param location string = 'eastus'

@description('Short environment tag used in resource names (dev, staging, prod).')
param environmentName string = 'dev'

@description('Name prefix applied to all resources.')
param namePrefix string = 'txv'

@secure()
@description('API key value stored in Key Vault as Security--ApiKey. Pass at deploy time, never commit a real value.')
param apiKeySecretValue string

var resourceGroupName = 'rg-${namePrefix}-${environmentName}'
var tags = {
  project: 'TransactionValidation'
  environment: environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module vnet 'modules/vnet.bicep' = {
  name: 'vnet'
  scope: rg
  params: {
    location: location
    vnetName: 'vnet-${namePrefix}-${environmentName}'
    tags: tags
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  scope: rg
  params: {
    location: location
    namespaceName: 'sb-${namePrefix}-${environmentName}-001'
    vnetId: vnet.outputs.vnetId
    peSubnetId: vnet.outputs.peSubnetId
    tags: tags
  }
}

module redis 'modules/redis.bicep' = {
  name: 'redis'
  scope: rg
  params: {
    location: location
    redisName: 'redis-${namePrefix}-${environmentName}'
    vnetId: vnet.outputs.vnetId
    peSubnetId: vnet.outputs.peSubnetId
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    keyVaultName: 'kv-${namePrefix}-${environmentName}'
    apiKeySecretValue: apiKeySecretValue
    redisConnectionStringSecretValue: redis.outputs.primaryConnectionString
    tags: tags
  }
}

module containerApps 'modules/containerapps.bicep' = {
  name: 'containerapps'
  scope: rg
  params: {
    location: location
    environmentName: 'cae-${namePrefix}-${environmentName}'
    logAnalyticsName: 'log-${namePrefix}-${environmentName}'
    acrName: replace('acr${namePrefix}${environmentName}', '-', '')
    infraSubnetId: vnet.outputs.infraSubnetId
    apiAppName: '${namePrefix}-api-${environmentName}'
    mockAppName: '${namePrefix}-mock-${environmentName}'
    serviceBusNamespaceId: serviceBus.outputs.namespaceId
    serviceBusNamespaceFqdn: serviceBus.outputs.namespaceFqdn
    keyVaultId: keyVault.outputs.keyVaultId
    keyVaultUri: keyVault.outputs.keyVaultUri
    tags: tags
  }
}

output resourceGroupName string = rg.name
output apiFqdn string = containerApps.outputs.apiFqdn
output mockFqdn string = containerApps.outputs.mockFqdn
output acrLoginServer string = containerApps.outputs.acrLoginServer
