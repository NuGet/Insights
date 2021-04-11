param storageAccountName string
param keyVaultName string
param identities array
param deploymentContainerName string = 'null'

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: storageAccountName
  location: resourceGroup().location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = if (deploymentContainerName != 'null') {
  name: '${storageAccountName}/default/${deploymentContainerName}'
  dependsOn: [
    storageAccount
  ]
}

resource deploymentContainerPolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2019-06-01' = if (deploymentContainerName != 'null') {
  name: '${storageAccountName}/default'
  dependsOn: [
    storageAccount
  ]
  properties: {
    policy: {
      rules: [
        {
          name: 'DeleteDeploymentBlobs'
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
                '${deploymentContainerName}/'
              ]
            }
          }
        }
      ]
    }
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2019-09-01' = {
  name: keyVaultName
  location: resourceGroup().location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: [for identity in identities: {
      tenantId: identity.tenantId
      objectId: identity.objectId
      permissions: {
        secrets: [
          'get'
        ]
      }
    }]
  }
}

resource keyVaultDiagnostics 'microsoft.insights/diagnosticSettings@2017-05-01-preview' = {
  scope: keyVault
  name: '${keyVaultName}-diagnostics'
  properties: {
    storageAccountId: storageAccount.id
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
    ]
  }
}
