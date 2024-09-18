param storageAccountName string
param deploymentContainerName string
param leaseContainerName string
param location string
param denyTraffic bool
param allowSharedKeyAccess bool

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-04-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    defaultToOAuthAuthentication: !allowSharedKeyAccess
    allowSharedKeyAccess: allowSharedKeyAccess
    networkAcls: {
      defaultAction: denyTraffic ? 'Deny' : 'Allow'
    }
  }
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = {
  name: '${storageAccountName}/default/${deploymentContainerName}'
  dependsOn: [
    storageAccount
  ]
}

resource leaseContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = {
  name: '${storageAccountName}/default/${leaseContainerName}'
  dependsOn: [
    storageAccount
  ]
}

resource leaseContainerPolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2019-06-01' = {
  parent: storageAccount
  name: 'default'
  dependsOn: [
    leaseContainer
  ]
  properties: {
    policy: {
      rules: [
        {
          name: 'DeleteOldBlobs'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                delete: {
                  daysAfterModificationGreaterThan: 1
                }
              }
            }
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                '${leaseContainerName}/'
              ]
            }
          }
        }
      ]
    }
  }
}

output storageVirtualNetworkRules array = storageAccount.properties.networkAcls.virtualNetworkRules
