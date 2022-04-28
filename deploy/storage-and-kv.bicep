param storageAccountName string
param keyVaultName string
param identities array
param deploymentContainerName string
param leaseContainerName string
param location string = resourceGroup().location

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

resource deploymentContainerPolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2019-06-01' = {
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

// Gives permissions to read certificates
resource keyVaultReadSecretPermissions 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for identity in identities: {
  name: !empty(identities) ? guid('AppsCanUseKeyVaultSecrets-${identity.tenantId}-${identity.objectId}') : guid('placeholderA')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: identity.objectId
    principalType: 'ServicePrincipal'
  }
}]

resource keyVaultReadPermissions 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for identity in identities: {
  name: !empty(identities) ? guid('AppsCanReadKeyVault-${identity.tenantId}-${identity.objectId}') : guid('placeholderB')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '21090545-7ca7-4776-b22c-e363652d74d2')
    principalId: identity.objectId
    principalType: 'ServicePrincipal'
  }
}]

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
