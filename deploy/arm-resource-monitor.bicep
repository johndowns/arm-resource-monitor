param location string = resourceGroup().location

param appNamePrefix string = uniqueString(resourceGroup().id)

var functionAppName = appNamePrefix
var appServicePlanName = '${appNamePrefix}-plan'
var appInsightsName = appNamePrefix
var storageAccountName = format('{0}fn', replace(appNamePrefix, '-', ''))
var resourceUpdatedQueueName = 'resource-updated'
var resourceUpdateErrorQueueName = 'resource-update-error'

// Storage account. This is used for the function app and for our queues.
resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: storageAccountName
  location: location
  sku: {
      name: 'Standard_LRS'
      tier: 'Standard'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    encryption: {
        services: {
            file: {
                keyType: 'Account'
                enabled: true
            }
            blob: {
                keyType: 'Account'
                enabled: true
            }
        }
        keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }
}

// Queues. Once Bicep has been updated to support copy loops, this could be simplified.
resource resourceUpdatedQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2019-06-01' = {
  name: '${storageAccount.name}/default/${resourceUpdatedQueueName}'
  properties: { }
}
resource resourceUpdateErrorQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2019-06-01' = {
  name: '${storageAccount.name}/default/${resourceUpdateErrorQueueName}'
  properties: { }
}

// Application Insights.
resource appInsights 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 90
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Consumption plan for Azure Functions.
resource appServicePlan 'Microsoft.Web/serverFarms@2019-08-01' = {
  name: appServicePlanName
  location: location
  kind: 'functionapp'
  sku: {
      name: 'Y1'
      tier: 'Dynamic'
      size: 'Y1'
      family: 'Y'
      capacity: 0
  }
}

// Function app.
resource functionApp 'Microsoft.Web/sites@2018-11-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    enabled: true
    serverFarmId: appServicePlan.id
    siteConfig: {
        appSettings: [
            {
                name: 'AzureWebJobsStorage'
                value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(storageAccount.id, '2019-06-01').keys[0].value}'
            }
            {
                name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
                value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(storageAccount.id, '2019-06-01').keys[0].value}'
            }
            {
                name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
                value: '${reference(appInsights.id, '2018-05-01-preview').InstrumentationKey}'
            }
            {
                name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
                value: 'InstrumentationKey=${reference(appInsights.id, '2018-05-01-preview').InstrumentationKey}'
            }
            {
                name: 'FUNCTIONS_WORKER_RUNTIME'
                value: 'dotnet'
            }
            {
                name: 'FUNCTIONS_EXTENSION_VERSION'
                value: '~3'
            }
            {
                name: 'SendGridApiKey'
                value: 'TODO'
            }
            {
                name: 'SendGridEmailFromAddress'
                value: 'TODO'
            }
            {
                name: 'SendGridEmailFromName'
                value: 'TODO'
            }
            {
                name: 'AlertEmailAddress'
                value: 'TODO'
            }
        ]
    }
  }
}
