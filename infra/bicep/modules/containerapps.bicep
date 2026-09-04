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
    type: 'SystemAssigned'
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
          identity: 'system'
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
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
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
          identity: 'system'
        }
      ]
      secrets: [
        {
          name: 'security-api-key'
          keyVaultUrl: '${keyVaultUri}secrets/Security--ApiKey'
          identity: 'system'
        }
        {
          name: 'redis-connection-string'
          keyVaultUrl: '${keyVaultUri}secrets/Redis--ConnectionString'
          identity: 'system'
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
}

// Role assignments below depend on apiApp/mockApp's system-assigned identity (created above);
// the initial revision may need to re-resolve Key Vault/ACR secrets once these propagate.
resource apiServiceBusSenderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceId, apiApp.id, serviceBusDataSenderRoleId)
  scope: existingServiceBusNamespace
  properties: {
    principalId: apiApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSenderRoleId)
    principalType: 'ServicePrincipal'
  }
}

resource mockServiceBusReceiverRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceId, mockApp.id, serviceBusDataReceiverRoleId)
  scope: existingServiceBusNamespace
  properties: {
    principalId: mockApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiverRoleId)
    principalType: 'ServicePrincipal'
  }
}

resource apiKeyVaultSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultId, apiAppName, keyVaultSecretsUserRoleId)
  scope: existingKeyVault
  properties: {
    principalId: apiApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalType: 'ServicePrincipal'
  }
}

resource apiAcrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, apiAppName, acrPullRoleId)
  scope: acr
  properties: {
    principalId: apiApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalType: 'ServicePrincipal'
  }
}

resource mockAcrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, mockAppName, acrPullRoleId)
  scope: acr
  properties: {
    principalId: mockApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalType: 'ServicePrincipal'
  }
}

output apiFqdn string = apiApp.properties.configuration.ingress.fqdn
output mockFqdn string = mockApp.properties.configuration.ingress.fqdn
output acrLoginServer string = acr.properties.loginServer
