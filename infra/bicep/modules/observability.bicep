@description('Azure region for observability resources.')
param location string

param logAnalyticsName string
param applicationInsightsName string
param tags object = {}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 5
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    RetentionInDays: 5
  }
}

output logAnalyticsName string = logAnalytics.name
output logAnalyticsId string = logAnalytics.id
output applicationInsightsName string = applicationInsights.name
@secure()
output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
