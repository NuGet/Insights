param storageAccountName string
param userManagedIdentityName string

param location string

param planName string
param sku string
param isLinux bool

param autoscaleName string
param minInstances int
param maxInstances int

param name string
@secure()
param zipUrl string
param hostId string
param logLevel string
param config array

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' existing = {
  name: storageAccountName
}

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userManagedIdentityName
}

var sakConnectionString = 'AccountName=${storageAccountName};AccountKey=${storageAccount.listkeys().keys[0].value};DefaultEndpointsProtocol=https;EndpointSuffix=${environment().suffixes.storage}'
var isConsumptionPlan = sku == 'Y1'

// See: https://learn.microsoft.com/en-us/azure/azure-functions/run-functions-from-deployment-package#using-website_run_from_package--url
// Also, I've see weird deployment timeouts or "Central directory corrupt" errors when using ZipDeploy on Linux.
var runFromZipUrl = isLinux

resource workerPlan 'Microsoft.Web/serverfarms@2020-09-01' = {
  name: planName
  location: location
  kind: 'functionapp'
  sku: {
    name: sku
  }
  properties: {
    reserved: isLinux
  }
}

resource workerPlanAutoScale 'Microsoft.Insights/autoscalesettings@2015-04-01' = if (!isConsumptionPlan) {
  name: autoscaleName
  location: location
  properties: {
    enabled: true
    targetResourceUri: workerPlan.id
    profiles: [
      {
        name: 'Scale based on CPU'
        capacity: {
          default: string(minInstances)
          minimum: string(minInstances)
          maximum: string(maxInstances)
        }
        rules: [
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricNamespace: 'microsoft.web/serverfarms'
              metricResourceUri: workerPlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT10M'
              timeAggregation: 'Average'
              operator: 'GreaterThan'
              threshold: 25
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              cooldown: 'PT1M'
              value: string(min(maxInstances - minInstances, 5))
            }
          }
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricNamespace: 'microsoft.web/serverfarms'
              metricResourceUri: workerPlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT10M'
              timeAggregation: 'Average'
              operator: 'LessThan'
              threshold: 15
            }
            scaleAction: {
              direction: 'Decrease'
              type: 'ChangeCount'
              cooldown: 'PT2M'
              value: string(min(maxInstances - minInstances, 10))
            }
          }
        ]
      }
    ]
  }
}

var workerConfigWithStorage = concat(isConsumptionPlan ? [
    {
      name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
      // SAS-based connection strings don't work for this property
      value: sakConnectionString
    }
  ] : [], config)

resource worker 'Microsoft.Web/sites@2022-09-01' = {
  name: name
  location: location
  kind: isLinux ? 'functionapp,linux' : 'functionapp'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userManagedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: workerPlan.id
    clientAffinityEnabled: false
    httpsOnly: true
    siteConfig: union({
        minTlsVersion: '1.2'
        alwaysOn: !isConsumptionPlan
        use32BitWorkerProcess: false
        healthCheckPath: '/healthz'
        appSettings: concat([
            {
              name: 'AzureFunctionsJobHost__logging__LogLevel__Default'
              value: logLevel
            }
            {
              name: 'logging__LogLevel__Default'
              value: logLevel
            }
            {
              name: 'logging__ApplicationInsights__LogLevel__Default'
              value: logLevel
            }
            {
              name: 'AzureFunctionsWebHost__hostId'
              value: hostId
            }
            {
              name: 'AzureWebJobsStorage__accountName'
              value: storageAccountName
            }
            {
              name: 'AzureWebJobsStorage__credential'
              value: 'managedidentity'
            }
            {
              name: 'AzureWebJobsStorage__clientId'
              value: userManagedIdentity.properties.clientId
            }
            {
              name: 'FUNCTIONS_EXTENSION_VERSION'
              value: '~4'
            }
            {
              name: 'FUNCTIONS_WORKER_RUNTIME'
              value: 'dotnet-isolated'
            }
            {
              name: 'WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED'
              value: '1'
            }
            {
              name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
              value: 'false'
            }
            {
              name: 'QueueTriggerConnection__queueServiceUri'
              value: storageAccount.properties.primaryEndpoints.queue
            }
            {
              name: 'QueueTriggerConnection__credential'
              value: 'managedidentity'
            }
            {
              name: 'QueueTriggerConnection__clientId'
              value: userManagedIdentity.properties.clientId
            }
            {
              // See: https://github.com/projectkudu/kudu/wiki/Configurable-settings#ensure-update-site-and-update-siteconfig-to-take-effect-synchronously 
              name: 'WEBSITE_ENABLE_SYNC_UPDATE_SITE'
              value: '1'
            }
            {
              name: 'WEBSITE_RUN_FROM_PACKAGE'
              value: runFromZipUrl ? split(zipUrl, '?')[0] : '1'
            }
          ], runFromZipUrl ? [
            {
              name: 'WEBSITE_RUN_FROM_PACKAGE_BLOB_MI_RESOURCE_ID'
              value: userManagedIdentity.id
            }
          ] : [], workerConfigWithStorage)
      }, isLinux ? {
        linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      } : {
        netFrameworkVersion: 'v8.0'
      })
  }

  resource workerDeploy 'extensions' = if (!runFromZipUrl) {
    name: any('ZipDeploy') // Workaround per: https://github.com/Azure/bicep/issues/784#issuecomment-817260643
    properties: {
      packageUri: zipUrl
    }
  }
}
