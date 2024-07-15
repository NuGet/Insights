param location string

param websiteName string
param websiteLocation string
param deploymentScriptPrefix string

param workerPlanLocations array
@minValue(1)
param workerPlanCount int
@minValue(1)
param workerCountPerPlan int
param workerNamePrefix string

param useSpotWorkers bool
param spotWorkerSpecs array

// website vnet
var websiteNsgName = '${websiteName}-nsg'
var websiteVnetName = '${websiteName}-vnet'

resource websiteNsg 'Microsoft.Network/networkSecurityGroups@2021-03-01' = {
  name: websiteNsgName
  location: websiteLocation
  properties: {
    securityRules: []
  }
}

resource websiteVnet 'Microsoft.Network/virtualNetworks@2021-03-01' = {
  name: websiteVnetName
  location: websiteLocation
  properties: {
    addressSpace: {
      addressPrefixes: [
        '172.25.0.0/24'
      ]
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '172.25.0.0/25'
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage.Global'
              locations: [
                '*'
              ]
            }
          ]
          delegations: [
            {
              name: 'Microsoft.Web/serverfarms'
              properties: {
                serviceName: 'Microsoft.Web/serverfarms'
              }
            }
          ]
          networkSecurityGroup: {
            id: websiteNsg.id
          }
        }
      }
    ]
  }
}

output websiteSubnetId string = websiteVnet.properties.subnets[0].id

// function workers
var workerCount = workerPlanCount * workerCountPerPlan
var workersVnetDeploymentLongName = '${deployment().name}-function-worker-vnets-'

// Subtract 10 from the max length to account for the index appended to the module name
var workersVnetDeploymentName = length(workersVnetDeploymentLongName) > (64 - 10)
  ? '${guid(deployment().name)}-function-worker-vnetsx-'
  : workersVnetDeploymentLongName

module workerVnets './function-worker-vnet.bicep' = [
  for index in range(0, workerCount): {
    name: '${workersVnetDeploymentName}${index}'
    params: {
      location: workerPlanLocations[(index / workerCountPerPlan) % length(workerPlanLocations)]
      name: '${workerNamePrefix}${index}'
      index: index
    }
  }
]

output workerSubnetIds array = [for index in range(0, workerCount): workerVnets[index].outputs.subnetId]

// deployment script (e.g. for spot worker script upload)
var deploymentScriptNsgName = '${deploymentScriptPrefix}nsg'
var deploymentScriptVnetName = '${deploymentScriptPrefix}vnet'

resource deploymentScriptNsg 'Microsoft.Network/networkSecurityGroups@2021-03-01' = {
  name: deploymentScriptNsgName
  location: location
  properties: {
    securityRules: []
  }
}

resource deploymentScriptVnet 'Microsoft.Network/virtualNetworks@2021-03-01' = {
  name: deploymentScriptVnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '172.28.0.0/24'
      ]
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '172.28.0.0/25'
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage.Global'
              locations: [
                '*'
              ]
            }
          ]
          delegations: [
            {
              name: 'Microsoft.ContainerInstance.containerGroups'
              properties: {
                serviceName: 'Microsoft.ContainerInstance/containerGroups'
              }
            }
          ]
          networkSecurityGroup: {
            id: deploymentScriptNsg.id
          }
        }
      }
    ]
  }
}

output deploymentScriptSubnetId string = deploymentScriptVnet.properties.subnets[0].id

// spot workers
var spotWorkerVnetsDeploymentLongName = '${deployment().name}-spot-worker-vnets'
var spotWorkerVnetsDeploymentName = length(spotWorkerVnetsDeploymentLongName) > 64
  ? '${guid(deployment().name)}-spot-worker-vnets'
  : spotWorkerVnetsDeploymentLongName

module spotWorkerVnets './spot-worker-vnets.bicep' = if (useSpotWorkers) {
  name: spotWorkerVnetsDeploymentName
  params: {
    specs: spotWorkerSpecs
  }
}

output spotWorkerSubnetIds array = useSpotWorkers ? spotWorkerVnets.outputs.subnetIds : []
