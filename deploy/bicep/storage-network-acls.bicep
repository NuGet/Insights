param storageAccountName string
param location string
param subnetIds array
param denyTraffic bool
param allowedIpRanges array

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
      defaultAction: denyTraffic ? 'Deny' : 'Allow'
      bypass: 'AzureServices'
      virtualNetworkRules: [
        for subnetId in subnetIds: {
          id: subnetId
          action: 'Allow'
        }
      ]
      ipRules: [
        for item in allowedIpRanges: {
          value: item
        }
      ]
    }
  }
}
