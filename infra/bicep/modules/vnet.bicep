@description('Azure region for all resources.')
param location string

@description('Address space for the VNet.')
param addressPrefix string = '10.20.0.0/16'

@description('Subnet used for Container Apps environment VNet integration (delegated to Microsoft.App/environments).')
param infraSubnetPrefix string = '10.20.0.0/23'

@description('Subnet used for private endpoints (Service Bus, Redis).')
param peSubnetPrefix string = '10.20.2.0/27'

param vnetName string
param tags object = {}

// Declares the VNet resource; Azure creates or updates it to match this shape.
resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    // Overall IP range the VNet owns; subnets below must fall within it.
    addressSpace: {
      addressPrefixes: [addressPrefix]
    }
    subnets: [
      {
        // Subnet Container Apps' environment attaches to for VNet integration.
        name: 'snet-infra'
        properties: {
          addressPrefix: infraSubnetPrefix
          // Hands this subnet over to the Container Apps service; only it can place resources here.
          delegations: [
            {
              name: 'Microsoft.App.environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        // Subnet dedicated to private endpoints (Service Bus, Redis).
        name: 'snet-pe'
        properties: {
          addressPrefix: peSubnetPrefix
          // Required so private endpoint NICs can be created in this subnet.
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

// Passed to other modules so they can reference this VNet without redeclaring it.
output vnetId string = vnet.id
// Index 0 matches the first entry in the subnets array above (snet-infra).
output infraSubnetId string = vnet.properties.subnets[0].id
// Index 1 matches the second entry in the subnets array above (snet-pe).
output peSubnetId string = vnet.properties.subnets[1].id
