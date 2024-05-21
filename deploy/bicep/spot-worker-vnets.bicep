param specs array

// spot workers
var workersDeploymentLongName = '${deployment().name}-'

// Subtract 10 from the max length to account for the index appended to the module name
var workersDeploymentName = length(workersDeploymentLongName) > (64 - 10)
  ? '${guid(deployment().name)}-'
  : workersDeploymentLongName

module workers './spot-worker-vnet.bicep' = [
  for (spec, index) in specs: {
    name: '${workersDeploymentName}${index}'
    params: {
      location: spec.location
      nsgName: '${spec.namePrefix}nsg'
      vnetName: '${spec.namePrefix}vnet'
      index: index
    }
  }
]

output subnetIds array = [for (spec, index) in specs: workers[index].outputs.subnetId]
