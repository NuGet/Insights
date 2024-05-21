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
      // TODO: switch to 'Deny' when Kusto ingestion allow-list approach is figured out
      defaultAction: 'Allow'
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
