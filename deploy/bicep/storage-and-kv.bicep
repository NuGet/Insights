param storageAccountName string
param keyVaultName string
param leaseContainerName string
param location string

/*
HACK: This should be hard coded to false. Currently it must be while the spot worker deployment
script runs otherwise the associated container instance never starts.
Blocking issue: https://msazure.visualstudio.com/One/_workitems/edit/28104339
*/
param allowSharedKeyAccess bool = false

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
    defaultToOAuthAuthentication: true
    allowSharedKeyAccess: allowSharedKeyAccess
  }
}

resource leaseContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = {
  name: '${storageAccountName}/default/${leaseContainerName}'
  dependsOn: [
    storageAccount
  ]
}

resource keyVault 'Microsoft.KeyVault/vaults@2019-09-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    accessPolicies: []
  }
}

resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2017-05-01-preview' = {
  scope: keyVault
  name: '${keyVaultName}-diagnostics'
  properties: {
    storageAccountId: storageAccount.id
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
      }
    ]
  }
}

var auditContainerName = 'insights-logs-auditevent'

resource auditContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = {
  name: '${storageAccountName}/default/${auditContainerName}'
  dependsOn: [
    storageAccount
  ]
}

resource leaseContainerPolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2019-06-01' = {
  parent: storageAccount
  name: 'default'
  dependsOn: [
    leaseContainer
    auditContainer
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
        {
          name: 'DeleteOldAudits'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                delete: {
                  daysAfterModificationGreaterThan: 180
                }
              }
            }
            filters: {
              blobTypes: [
                'blockBlob'
                'appendBlob'
              ]
              prefixMatch: [
                '${auditContainerName}/'
              ]
            }
          }
        }
      ]
    }
  }
}

output storageVirtualNetworkRules array = storageAccount.properties.networkAcls.virtualNetworkRules
