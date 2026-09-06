@description('Azure region for all resources.')
param location string

param namespaceName string
param vnetId string
param peSubnetId string
param tags object = {}

// Private Link for Service Bus requires the Premium tier (Standard/Basic do not support private endpoints).
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: 'Premium'
    tier: 'Premium'
    capacity: 1
  }
  properties: {
    disableLocalAuth: false
  }
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: serviceBusNamespace
  name: 'partner.transactions'
  properties: {
    defaultMessageTimeToLive: 'P14D'
  }
}

resource primarySubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: topic
  name: 'partner-transactions'
  properties: {
    lockDuration: 'PT30S'
    maxDeliveryCount: 10
  }
}

resource primaryFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: primarySubscription
  name: 'primary-match'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: {
      sqlExpression: 'eventType IN (\'partner.transaction.accepted\',\'partner.transaction.rejected\',\'partner.transaction.pending\')'
    }
  }
}

resource auditSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: topic
  name: 'partner-transactions.audit'
  properties: {
    lockDuration: 'PT30S'
    maxDeliveryCount: 10
  }
}

resource auditFilter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: auditSubscription
  name: 'audit-match'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: {
      sqlExpression: 'eventType = \'partner.transaction.accepted\''
    }
  }
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.servicebus.windows.net'
  location: 'global'
  tags: tags
}

resource privateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZone
  name: '${namespaceName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-${namespaceName}'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: peSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'pe-${namespaceName}-connection'
        properties: {
          privateLinkServiceId: serviceBusNamespace.id
          groupIds: ['namespace']
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
        name: 'servicebus'
        properties: {
          privateDnsZoneId: privateDnsZone.id
        }
      }
    ]
  }
}

output namespaceId string = serviceBusNamespace.id
output namespaceName string = serviceBusNamespace.name
output namespaceFqdn string = '${serviceBusNamespace.name}.servicebus.windows.net'
