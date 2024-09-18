param keyVaultName string
param userManagedIdentityName string
param deploymentLabel string
param storageAccountName string
param deploymentContainerName string
param customScriptExtensionFiles array
param appInsightsName string

param adminUsername string
@secure()
param adminPassword string
param specs array // An array of objects with these properties: "namePrefix", "location", "sku", "minInstances", "maxInstances"
param subnetIds array
param imageReference object
param enableAutomaticOSUpgrade bool

param deploymentTimestamp string = utcNow()

resource keyVault 'Microsoft.KeyVault/vaults@2019-09-01' existing = {
  name: keyVaultName

  resource adminPasswordSecret 'secrets' = {
    name: 'admin-password'
    properties: {
      contentType: 'text/plain'
      value: adminPassword
      attributes: {
        nbf: dateTimeToEpoch(deploymentTimestamp) // makes it easier to see old secret versions sorted by time
      }
    }
  }
}

// Compute the directory name of for the deployment files. This should be the directory name of the
// blob, without the container name, per:
// https://learn.microsoft.com/en-us/azure/virtual-machines/extensions/custom-script-windows#troubleshoot-and-support
resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' existing = {
  name: storageAccountName
}
var deploymentContainerUrl = '${storageAccount.properties.primaryEndpoints.blob}${deploymentContainerName}/'
var filePath = substring(customScriptExtensionFiles[0], length(deploymentContainerUrl))
var firstSlashIndex = indexOf(filePath, '/')
var customScriptExtensionFileDirectory = firstSlashIndex > 0 ? substring(filePath, 0, firstSlashIndex + 1) : ''

var workersDeploymentLongName = '${deployment().name}-'

// Subtract 10 from the max length to account for the index appended to the module name
var workersDeploymentName = length(workersDeploymentLongName) > (64 - 10)
  ? '${guid(deployment().name)}-'
  : workersDeploymentLongName

module workers './spot-worker.bicep' = [
  for (spec, index) in specs: {
    name: '${workersDeploymentName}${index}'
    params: {
      userManagedIdentityName: userManagedIdentityName
      appInsightsName: appInsightsName
      deploymentLabel: deploymentLabel
      customScriptExtensionFileDirectory: customScriptExtensionFileDirectory
      customScriptExtensionFiles: customScriptExtensionFiles
      location: spec.location
      vmssSku: spec.sku
      vmssName: '${spec.namePrefix}vmss'
      minInstances: spec.minInstances
      maxInstances: spec.maxInstances
      nicName: '${spec.namePrefix}nic'
      ipConfigName: '${spec.namePrefix}ip'
      loadBalancerName: '${spec.namePrefix}lb'
      autoscaleName: '${spec.namePrefix}autoscale'
      adminUsername: adminUsername
      adminPassword: adminPassword
      addLoadBalancer: spec.addLoadBalancer
      subnetId: subnetIds[index]
      imageReference: imageReference
      enableAutomaticOSUpgrade: enableAutomaticOSUpgrade
    }
  }
]
