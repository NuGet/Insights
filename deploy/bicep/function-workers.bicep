param storageAccountName string
param userManagedIdentityName string

param planNamePrefix string
param planLocations array
@minValue(1)
param planCount int
@minValue(1)
param countPerPlan int
param sku string

param autoscaleNamePrefix string
param minInstances int
param maxInstances int

param namePrefix string
@secure()
param zipUrl string
param hostId string
param logLevel string
param config array

var workerCount = planCount * countPerPlan

var workersDeploymentLongName = '${deployment().name}-function-worker-'

// Subtract 10 from the max length to account for the index appended to the module name
var workersDeploymentName = length(workersDeploymentLongName) > (64 - 10) ? '${guid(deployment().name)}-function-worker-' : workersDeploymentLongName

module workers './function-worker.bicep' = [for i in range(0, workerCount): {
  name: '${workersDeploymentName}${i}'
  params: {
    storageAccountName: storageAccountName
    userManagedIdentityName: userManagedIdentityName
    location: planLocations[(i / countPerPlan) % length(planLocations)]
    planName: '${planNamePrefix}${i / countPerPlan}'
    sku: sku
    autoscaleName: '${autoscaleNamePrefix}${i / countPerPlan}'
    minInstances: minInstances
    maxInstances: maxInstances
    name: '${namePrefix}${i}'
    zipUrl: zipUrl
    hostId: hostId
    logLevel: logLevel
    config: config
  }
}]
