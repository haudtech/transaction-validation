@description('Azure region for all resources.')
param location string

param redisName string
param vnetId string
param peSubnetId string
param tags object = {}

// Basic C0 tier: lowest cost for a single dev/POC environment; private endpoints are supported on all tiers.
resource redis 'Microsoft.Cache/redis@2024-11-01' = {
  name: redisName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0
    }
    publicNetworkAccess: 'Disabled'
    minimumTlsVersion: '1.2'
  }
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.redis.cache.windows.net'
  location: 'global'
  tags: tags
}

resource privateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZone
  name: '${redisName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-${redisName}'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: peSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'pe-${redisName}-connection'
        properties: {
          privateLinkServiceId: redis.id
          groupIds: ['redisCache']
        }
      }
    ]
  }
}

resource privateEndpointDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'redis'
        properties: {
          privateDnsZoneId: privateDnsZone.id
        }
      }
    ]
  }
}

output redisId string = redis.id
output redisHostName string = redis.properties.hostName

@secure()
output primaryConnectionString string = '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
