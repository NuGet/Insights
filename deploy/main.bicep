// Parameters
param stackName string

param storageAccountName string
param storageKeySecretName string
param sasDefinitionName string

param keyVaultName string

param websitePlanId string
param websiteAadClientId string
param websiteConfig array

param workerConfig array
@allowed([
  'Warning'
  'Information'
])
param workerLogLevel string = 'Warning'
@minValue(1)
param workerCount int
param existingWorkerCount int

// Shared resources
resource insights 'Microsoft.Insights/components@2015-05-01' = {
  name: 'ExplorePackages-${stackName}'
  location: resourceGroup().location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' existing = {
  name: storageAccountName
}

// Cannot use a KeyVault reference for initial deployment.
// https://github.com/Azure/azure-functions-host/issues/7094
var storageSecretValue = 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${listkeys(storageAccount.id, storageAccount.apiVersion).keys[0].value};EndpointSuffix=core.windows.net'
var storageSecretReference = '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=${storageKeySecretName})'
var workerSecret = existingWorkerCount >= workerCount ? storageSecretReference : storageSecretValue

output needsAnotherDeploy bool = workerSecret != storageSecretReference
output websiteDefaultHostName string = website.properties.defaultHostName
output websiteHostNames array = website.properties.hostNames
output websiteId string = website.id
output workerIds array = [for i in range(0, workerCount): workers[i].id]

var sharedConfig = [
  {
    name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
    value: insights.properties.InstrumentationKey
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: insights.properties.ConnectionString
  }
  {
    name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
    value: '~2'
  }
  {
    name: 'Knapcode.ExplorePackages:StorageAccountName'
    value: storageAccountName
  }
  {
    name: 'Knapcode.ExplorePackages:StorageSharedAccessSignature'
    value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=${storageAccountName}-${sasDefinitionName})'
  }
  {
    name: 'WEBSITE_RUN_FROM_PACKAGE'
    value: '1'
  }
]

resource keyVault 'Microsoft.KeyVault/vaults@2019-09-01' = {
  name: keyVaultName
  location: resourceGroup().location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: [for i in range(0, workerCount + 1): {
      tenantId: i == 0 ? website.identity.tenantId : workers[i - 1].identity.tenantId
      objectId: i == 0 ? website.identity.principalId : workers[i - 1].identity.principalId
      permissions: {
        secrets: [
          'get'
        ]
      }
    }]
  }
}
  
resource keyVaultDiagnostics 'microsoft.insights/diagnosticSettings@2017-05-01-preview' = {
  scope: keyVault
  name: '${keyVaultName}-diagnostics'
  properties: {
    storageAccountId: storageAccount.id
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
    ]
  }
}

// Website
resource website 'Microsoft.Web/sites@2020-09-01' = {
  name: 'ExplorePackages-${stackName}'
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: websitePlanId
    clientAffinityEnabled: false
    httpsOnly: true
    siteConfig: {
      webSocketsEnabled: true
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v5.0'
      appSettings: concat([
        {
          name: 'AzureAd:Instance'
          value: 'https://login.microsoftonline.com/'
        }
        {
          name: 'AzureAd:ClientId'
          value: websiteAadClientId
        }
        {
          name: 'AzureAd:TenantId'
          value: 'common'
        }
      ], sharedConfig, websiteConfig)
    }
  }
}

// Workers
resource workerPlan 'Microsoft.Web/serverfarms@2020-09-01' = {
  name: 'ExplorePackages-${stackName}-WorkerPlan'
  location: resourceGroup().location
  sku: {
    name: 'Y1'
  }
}

resource workers 'Microsoft.Web/sites@2020-09-01' = [for i in range(0, workerCount): {
  name: 'ExplorePackages-${stackName}-Worker-${i}'
  location: resourceGroup().location
  kind: 'FunctionApp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: workerPlan.id
    clientAffinityEnabled: false
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      appSettings: concat([
        {
          name: 'AzureFunctionsJobHost__logging__LogLevel__Default'
          value: workerLogLevel
        }
        {
          name: 'AzureWebJobsFeatureFlags'
          value: 'EnableEnhancedScopes'
        }
        {
          name: 'AzureWebJobsStorage'
          value: workerSecret
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: workerSecret
        }
      ], sharedConfig, workerConfig)
    }
  }
}]
