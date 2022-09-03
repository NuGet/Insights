param storageAccountName string
param keyVaultName string
param userManagedIdentityName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' existing = {
  name: storageAccountName
}

resource keyVault 'Microsoft.KeyVault/vaults@2019-09-01' existing = {
  name: keyVaultName
}

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userManagedIdentityName
}

// Storage
resource blobPermissions 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = {
  name: guid('AppCanAccessBlob-${userManagedIdentity.id}')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: userManagedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource queuePermissions 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = {
  name: guid('AppCanAccessQueue-${userManagedIdentity.id}')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
    principalId: userManagedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource tablePermissions 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = {
  name: guid('AppCanAccessTable-${userManagedIdentity.id}')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
    principalId: userManagedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault
resource keyVaultReadSecretPermissions 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = {
  name: guid('AppCanUseKeyVaultSecretsAndCertificates-${userManagedIdentity.id}')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: userManagedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultReadPermissions 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = {
  name: guid('AppCanReadKeyVault-${userManagedIdentity.id}')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '21090545-7ca7-4776-b22c-e363652d74d2')
    principalId: userManagedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
