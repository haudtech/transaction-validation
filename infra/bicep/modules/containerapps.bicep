@description('Azure region for all resources.')
param location string

param environmentName string
param logAnalyticsName string
param acrName string
param infraSubnetId string
param apiAppName string
param mockAppName string

@description('Placeholder image until the CI/CD pipeline (Phase 6) pushes real images.')
param apiImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'
param mockImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

param serviceBusNamespaceId string
param serviceBusNamespaceFqdn string
param keyVaultId string
param keyVaultUri string
param tags object = {}

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var serviceBusDataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var serviceBusDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

resource existingServiceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: last(split(serviceBusNamespaceId, '/'))
}

resource existingKeyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: last(split(keyVaultId, '/'))
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

// User-assigned identities are created before the apps so their ACR/Key Vault roles already exist when
// the first revision provisions. A system-assigned identity cannot: its role assignments depend on the
// app, but the app's first image pull happens before those assignments can be created.
resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${apiAppName}'
  location: location
  tags: tags
}

resource mockIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${mockAppName}'
  location: location
  tags: tags
}

resource apiServiceBusSenderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceId, apiIdentity.id, serviceBusDataSenderRoleId)
  scope: existingServiceBusNamespace
  properties: {
    principalId: apiIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSenderRoleId)
    principalType: 'ServicePrincipal'
  }
}

resource mockServiceBusReceiverRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceId, mockIdentity.id, serviceBusDataReceiverRoleId)
  scope: existingServiceBusNamespace
  properties: {
    principalId: mockIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiverRoleId)
    principalType: 'ServicePrincipal'
  }
}

resource apiKeyVaultSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultId, apiIdentity.id, keyVaultSecretsUserRoleId)
  scope: existingKeyVault
  properties: {
    principalId: apiIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalType: 'ServicePrincipal'
  }
}

resource apiAcrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, apiIdentity.id, acrPullRoleId)
  scope: acr
  properties: {
    principalId: apiIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalType: 'ServicePrincipal'
  }
}

resource mockAcrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, mockIdentity.id, acrPullRoleId)
  scope: acr
  properties: {
    principalId: mockIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalType: 'ServicePrincipal'
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    vnetConfiguration: {
      infrastructureSubnetId: infraSubnetId
      internal: false
    }
  }
}

resource mockApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: mockAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${mockIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: mockIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'mock'
          image: mockImage
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'MESSAGING__BROKERTYPE', value: 'AzureServiceBus' }
            // DefaultAzureCredential requires this to select the user-assigned identity.
            { name: 'AZURE_CLIENT_ID', value: mockIdentity.properties.clientId }
            { name: 'SERVICEBUSCONSUMER__NAMESPACE', value: serviceBusNamespaceFqdn }
            { name: 'SERVICEBUSAUDITCONSUMER__NAMESPACE', value: serviceBusNamespaceFqdn }
            { name: 'SERVICEBUSCONSUMER__ENABLED', value: 'true' }
            { name: 'SERVICEBUSAUDITCONSUMER__ENABLED', value: 'true' }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
  dependsOn: [
    mockAcrPullRoleAssignment
    mockServiceBusReceiverRoleAssignment
  ]
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: apiIdentity.id
        }
      ]
      secrets: [
        {
          name: 'security-api-key'
          keyVaultUrl: '${keyVaultUri}secrets/Security--ApiKey'
          identity: apiIdentity.id
        }
        {
          name: 'redis-connection-string'
          keyVaultUrl: '${keyVaultUri}secrets/Redis--ConnectionString'
          identity: apiIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'MESSAGING__BROKERTYPE', value: 'AzureServiceBus' }
            // DefaultAzureCredential requires this to select the user-assigned identity.
            { name: 'AZURE_CLIENT_ID', value: apiIdentity.properties.clientId }
            { name: 'SERVICEBUSPUBLISHER__NAMESPACE', value: serviceBusNamespaceFqdn }
            { name: 'PARTNERVERIFICATION__BASEURL', value: 'http://${mockApp.properties.configuration.ingress.fqdn}/' }
            { name: 'SECURITY__APIKEY', secretRef: 'security-api-key' }
            { name: 'REDIS__CONNECTIONSTRING', secretRef: 'redis-connection-string' }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
  dependsOn: [
    apiAcrPullRoleAssignment
    apiKeyVaultSecretsUserRoleAssignment
    apiServiceBusSenderRoleAssignment
  ]
}

output apiFqdn string = apiApp.properties.configuration.ingress.fqdn
output mockFqdn string = mockApp.properties.configuration.ingress.fqdn
output acrLoginServer string = acr.properties.loginServer
output acrName string = acr.name
output apiAppName string = apiApp.name
output mockAppName string = mockApp.name
