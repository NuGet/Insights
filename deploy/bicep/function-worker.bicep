param storageAccountName string
param userManagedIdentityName string

param location string

param planName string
param sku string

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

var sakConnectionString = 'AccountName=${storageAccountName};AccountKey=${listkeys(storageAccount.id, storageAccount.apiVersion).keys[0].value};DefaultEndpointsProtocol=https;EndpointSuffix=${environment().suffixes.storage}'
var isConsumptionPlan = sku == 'Y1'

resource workerPlan 'Microsoft.Web/serverfarms@2020-09-01' = {
  name: planName
  location: location
  sku: {
    name: sku
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
              threshold: 15
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
              metricResourceUri: workerPlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'LessThan'
              threshold: 5
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
}

var workerConfigWithStorage = concat(isConsumptionPlan ? [
  {
    name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
    // SAS-based connection strings don't work for this property
    value: sakConnectionString
  }
] : [], config)

resource worker 'Microsoft.Web/sites@2020-09-01' = {
  name: name
  location: location
  kind: 'FunctionApp'
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
    siteConfig: {
      minTlsVersion: '1.2'
      alwaysOn: !isConsumptionPlan
      use32BitWorkerProcess: false
      appSettings: concat([
        {
          name: 'AzureFunctionsJobHost__logging__LogLevel__Default'
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
          value: 'dotnet'
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
      ], workerConfigWithStorage)
    }
  }

  resource workerDeployments 'extensions@2020-09-01' = {
    name: any('ZipDeploy')
    properties: {
      packageUri: zipUrl
    }
  }
}
