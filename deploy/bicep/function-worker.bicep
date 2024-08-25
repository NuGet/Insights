param userManagedIdentityName string

param location string

param planName string
param sku string
param isLinux bool
param runFromZipUrl bool
param isConsumptionPlan bool

param autoscaleName string
param minInstances int
param maxInstances int

param name string
@secure()
param zipUrl string
param config object
param subnetId string

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userManagedIdentityName
}

var appSettings = [
  for item in items(config): {
    name: item.key
    value: item.value
  }
]

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
    virtualNetworkSubnetId: isConsumptionPlan ? null : subnetId
    siteConfig: union(
      {
        minTlsVersion: '1.2'
        alwaysOn: !isConsumptionPlan
        use32BitWorkerProcess: false
        healthCheckPath: '/healthz'
        appSettings: appSettings
      },
      isLinux
        ? {
            linuxFxVersion: 'DOTNET-ISOLATED|8.0'
          }
        : {
            netFrameworkVersion: 'v8.0'
          }
    )
  }

  resource workerDeploy 'extensions' = if (!runFromZipUrl) {
    name: any('ZipDeploy') // Workaround per: https://github.com/Azure/bicep/issues/784#issuecomment-817260643
    properties: {
      packageUri: zipUrl
    }
  }
}
