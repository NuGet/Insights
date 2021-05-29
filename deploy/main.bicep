// Parameters
param appInsightsName string
param appInsightsDailyCapGb int
param storageAccountName string
param keyVaultName string
param deploymentContainerName string
param leaseContainerName string

param sasConnectionStringSecretName string
param appSasDefinitionName string
param blobReadSasDefinitionName string
param sasValidityPeriod string

param websitePlanId string = 'new'
param websitePlanName string = 'default'
param websiteName string
param websiteAadClientId string
param websiteConfig array
@secure()
param websiteZipUrl string

param workerPlanNamePrefix string
param workerUserManagedIdentityName string
param workerNamePrefix string
@minValue(1)
param workerPlanCount int
@minValue(1)
param workerCountPerPlan int
param workerConfig array
param workerLogLevel string = 'Warning'
param workerMinInstances int
param workerSku string = 'Y1'
@secure()
param workerZipUrl string

var sakConnectionString = 'AccountName=${storageAccountName};AccountKey=${listkeys(storageAccount.id, storageAccount.apiVersion).keys[0].value};DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net'
var sasConnectionStringReference = '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=${sasConnectionStringSecretName})'
var isConsumptionPlan = workerSku == 'Y1'
var isPremiumPlan = startsWith(workerSku, 'P')
var workerMaxInstances = isPremiumPlan ? 30 : 10
var workerCount = workerPlanCount * workerCountPerPlan

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
    // For the app settings to be different each time so that Key Vault references are reloaded
    name: 'ForceKeyVaultReferencesToReload'
    value: deployment().name
  }
  {
    name: 'NuGet.Insights:HostSubscriptionId'
    value: subscription().subscriptionId
  }
  {
    name: 'NuGet.Insights:HostResourceGroupName'
    value: resourceGroup().name
  }
  {
    name: 'NuGet.Insights:LeaseContainerName'
    value: leaseContainerName
  }
  {
    name: 'NuGet.Insights:KeyVaultName'
    value: keyVaultName
  }
  {
    name: 'NuGet.Insights:StorageAccountName'
    value: storageAccountName
  }
  {
    name: 'NuGet.Insights:StorageConnectionStringSecretName'
    value: sasConnectionStringSecretName
  }
  {
    name: 'NuGet.Insights:StorageSharedAccessSignatureSecretName'
    value: '${storageAccountName}-${appSasDefinitionName}'
  }
  {
    name: 'NuGet.Insights:StorageBlobReadSharedAccessSignatureSecretName'
    value: '${storageAccountName}-${blobReadSasDefinitionName}'
  }
  {
    name: 'NuGet.Insights:StorageSharedAccessSignatureDuration'
    value: sasValidityPeriod
  }
  {
    // See: https://github.com/projectkudu/kudu/wiki/Configurable-settings#ensure-update-site-and-update-siteconfig-to-take-effect-synchronously 
    name: 'WEBSITE_ENABLE_SYNC_UPDATE_SITE'
    value: '1'
  }
  {
    name: 'WEBSITE_RUN_FROM_PACKAGE'
    value: '1'
  }
]

// Shared resources
var storageAndKvLongName = '${deployment().name}-storage-and-kv'
var storageAndKvName = length(storageAndKvLongName) > 64 ? '${guid(deployment().name)}-storage-and-kv' : storageAndKvLongName
module storageAndKv './storage-and-kv.bicep' = {
  name: storageAndKvName
  params: {
    storageAccountName: storageAccountName
    keyVaultName: keyVaultName
    identities: [for i in range(0, workerCount + 1): {
      tenantId: i == 0 ? website.identity.tenantId : workers[i - 1].identity.tenantId
      objectId: i == 0 ? website.identity.principalId : workers[i - 1].identity.principalId
    }]
    deploymentContainerName: deploymentContainerName
    leaseContainerName: leaseContainerName
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' existing = {
  name: storageAccountName
}

resource insights 'Microsoft.Insights/components@2015-05-01' = {
  name: appInsightsName
  location: resourceGroup().location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }

  // This produces a warning due to limited type definitions, but works.
  // See: https://github.com/Azure/bicep/issues/784#issuecomment-830997209
  resource billing 'CurrentBillingFeatures' = {
    name: 'Basic'
    properties: {
      CurrentBillingFeatures: 'Basic'
      DataVolumeCap: {
        Cap: appInsightsDailyCapGb
        WarningThreshold: 90
      }
    }
  }
}

// Website
resource websitePlan 'Microsoft.Web/serverfarms@2020-09-01' = if (websitePlanId == 'new') {
  name: websitePlanName == 'default' ? '${websiteName}-WebsitePlan' : websitePlanName
  location: resourceGroup().location
  sku: {
    name: 'B1'
  }
}

resource website 'Microsoft.Web/sites@2020-09-01' = {
  name: websiteName
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: websitePlanId == 'new' ? websitePlan.id : websitePlanId
    clientAffinityEnabled: false
    httpsOnly: true
    siteConfig: {
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
        {
          // Needed so that the update secrets timer appears enabled in the UI
          name: 'NuGet.Insights:HostAppName'
          value: websiteName
        }
      ], sharedConfig, websiteConfig)
    }
  }

  resource deploy 'extensions' = {
    name: any('ZipDeploy') // Workaround per: https://github.com/Azure/bicep/issues/784#issuecomment-817260643
    properties: {
      packageUri: websiteZipUrl
    }
  }
}

// Workers
resource workerPlans 'Microsoft.Web/serverfarms@2020-09-01' = [for i in range(0, workerPlanCount): {
  name: '${workerPlanNamePrefix}${i}'
  location: resourceGroup().location
  sku: {
    name: workerSku
  }
}]

resource workerPlanAutoScale 'microsoft.insights/autoscalesettings@2015-04-01' = [for i in range(0, workerPlanCount): if (!isConsumptionPlan) {
  name: '${workerPlanNamePrefix}${i}'
  location: resourceGroup().location
  dependsOn: [
    workerPlans[i]
  ]
  properties: {
    enabled: true
    targetResourceUri: workerPlans[i].id
    profiles: [
      {
        name: 'Scale based on CPU'
        capacity: {
          default: string(workerMinInstances)
          minimum: string(workerMinInstances)
          maximum: string(workerMaxInstances)
        }
        rules: [
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricNamespace: 'microsoft.web/serverfarms'
              metricResourceUri: workerPlans[i].id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT10M'
              timeAggregation: 'Average'
              operator: 'GreaterThan'
              threshold: 66
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              value: '5'
              cooldown: 'PT3M'
            }
          }
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricNamespace: 'microsoft.web/serverfarms'
              metricResourceUri: workerPlans[i].id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'LessThan'
              threshold: 33
            }
            scaleAction: {
              direction: 'Decrease'
              type: 'ChangeCount'
              value: '5'
              cooldown: 'PT1M'
            }
          }
        ]
      }
    ]
  }
}]

var workerConfigWithStorage = concat(workerConfig, isConsumptionPlan ? [
  {
    name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
    // SAS-based connection strings don't work for this property
    value: sakConnectionString
  }
] : [])

resource workerUserManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: workerUserManagedIdentityName
  location: resourceGroup().location
}

resource workers 'Microsoft.Web/sites@2020-09-01' = [for i in range(0, workerCount): {
  name: '${workerNamePrefix}${i}'
  location: resourceGroup().location
  dependsOn: [
    workerUserManagedIdentity
  ]
  kind: 'FunctionApp'
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${workerUserManagedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: workerPlans[i / workerCountPerPlan].id
    clientAffinityEnabled: false
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      alwaysOn: !isConsumptionPlan
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
          value: sasConnectionStringReference
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
          name: 'NuGet.Insights:HostAppName'
          value: '${workerNamePrefix}${i}'
        }
        {
          name: 'NuGet.Insights:UserManagedIdentityClientId'
          value: workerUserManagedIdentity.properties.clientId
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
      ], sharedConfig, workerConfigWithStorage)
    }
  }
}]

resource resourceGroupPermissions 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for i in range(0, workerCount): {
  name: guid('FunctionsCanRestartThemselves-${workers[i].id}')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'de139f84-1756-47ae-9be6-808fbbe84772')
    principalId: workers[i].identity.principalId
    principalType: 'ServicePrincipal'
  }
}]

resource workerDeployments 'Microsoft.Web/sites/extensions@2020-09-01' = [for i in range(0, workerCount): {
  name: 'ZipDeploy'
  parent: workers[i]
  properties: {
    packageUri: workerZipUrl
  }
}]
