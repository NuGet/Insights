param storageAccountName string
param location string
param subnetIds array

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-04-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    networkAcls: {
      // Kusto ingestion needs manual setup of a private endpoint to this storage account.
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      virtualNetworkRules: [
        for subnetId in subnetIds: {
          id: subnetId
          action: 'Allow'
        }
      ]
    }
  }
}
