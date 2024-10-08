param userManagedIdentityName string

param planNamePrefix string
param planLocations array
@minValue(1)
param planCount int
@minValue(1)
param countPerPlan int
param sku string
param isLinux bool
param runFromZipUrl bool
param isConsumptionPlan bool

param autoscaleNamePrefix string
param minInstances int
param maxInstances int

param namePrefix string
@secure()
param zipUrl string
param config object
param subnetIds array

var workerCount = planCount * countPerPlan

var workersDeploymentLongName = '${deployment().name}-'

// Subtract 10 from the max length to account for the index appended to the module name
var workersDeploymentName = length(workersDeploymentLongName) > (64 - 10)
  ? '${guid(deployment().name)}-'
  : workersDeploymentLongName

module workers './function-worker.bicep' = [
  for index in range(0, workerCount): {
    name: '${workersDeploymentName}${index}'
    params: {
      userManagedIdentityName: userManagedIdentityName
      location: planLocations[(index / countPerPlan) % length(planLocations)]
      planName: '${planNamePrefix}${index / countPerPlan}'
      sku: sku
      isLinux: isLinux
      autoscaleName: '${autoscaleNamePrefix}${index / countPerPlan}'
      minInstances: minInstances
      maxInstances: maxInstances
      name: '${namePrefix}${index}'
      zipUrl: zipUrl
      config: config
      subnetId: subnetIds[index]
      runFromZipUrl: runFromZipUrl
      isConsumptionPlan: isConsumptionPlan
    }
  }
]
