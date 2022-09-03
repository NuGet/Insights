param storageAccountName string
param keyVaultName string
param leaseContainerName string
param location string

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource leaseContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = {
  name: '${storageAccountName}/default/${leaseContainerName}'
  dependsOn: [
    storageAccount
  ]
}

resource containerPolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2019-06-01' = {
  name: '${storageAccountName}/default'
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
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
    ]
  }
}
