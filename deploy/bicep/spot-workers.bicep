param location string
param storageAccountName string
param keyVaultName string
param userManagedIdentityName string
@secure()
param uploadScriptUrl string
@secure()
param deploymentUrls object // An object with a single property "files" which is an array of strings
param deploymentLabel string
param spotWorkerDeploymentContainerName string
param appInsightsName string

param adminUsername string
@secure()
param adminPassword string
param specs array // An array of objects with these properties: "namePrefix", "location", "sku", "minInstances", "maxInstances"

param deploymentTimestamp string = utcNow()

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userManagedIdentityName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' existing = {
  name: storageAccountName
}

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

resource leaseContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = {
  name: '${storageAccountName}/default/${spotWorkerDeploymentContainerName}'
  dependsOn: [
    storageAccount
  ]
}

resource uploadBlobs 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: '${storageAccountName}-spot-worker-upload'
  location: location
  kind: 'AzurePowerShell'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userManagedIdentity.id}': {}
    }
  }
  properties: {
    azPowerShellVersion: '7.5'
    arguments: '-ManagedIdentityClientId \'${userManagedIdentity.properties.clientId}\' -DeploymentLabel \'${deploymentLabel}\' -StorageAccountName \'${storageAccountName}\' -SpotWorkerDeploymentContainerName \'${spotWorkerDeploymentContainerName}\''
    primaryScriptUri: uploadScriptUrl
    supportingScriptUris: concat(deploymentUrls.files, [
        'https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.ps1'
      ])
    cleanupPreference: 'Always'
    retentionInterval: 'PT1H'
  }
  dependsOn: [
    leaseContainer
  ]
}

var workersDeploymentLongName = '${deployment().name}-spot-worker-'

// Subtract 10 from the max length to account for the index appended to the module name
var workersDeploymentName = length(workersDeploymentLongName) > (64 - 10) ? '${guid(deployment().name)}-spot-worker-' : workersDeploymentLongName

module workers './spot-worker.bicep' = [for (spec, index) in specs: {
  name: '${workersDeploymentName}${index}'
  params: {
    userManagedIdentityName: userManagedIdentityName
    appInsightsName: appInsightsName
    deploymentLabel: deploymentLabel
    customScriptExtensionFiles: uploadBlobs.properties.outputs.customScriptExtensionFiles
    location: spec.location
    vmssSku: spec.sku
    nsgName: '${spec.namePrefix}nsg'
    vnetName: '${spec.namePrefix}vnet'
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
  }
}]
