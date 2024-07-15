param location string
param nsgName string
param vnetName string
param index int

resource nsg 'Microsoft.Network/networkSecurityGroups@2021-03-01' = {
  name: nsgName
  location: location
  properties: {
    securityRules: [
      {
        name: 'AllowCorpNetPublicRdp'
        properties: {
          priority: 2000
          protocol: 'Tcp'
          access: 'Allow'
          direction: 'Inbound'
          sourceAddressPrefix: 'CorpNetPublic'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '3389'
        }
      }
      {
        name: 'AllowCorpNetSawRdp'
        properties: {
          priority: 2001
          protocol: 'Tcp'
          access: 'Allow'
          direction: 'Inbound'
          sourceAddressPrefix: 'CorpNetSaw'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '3389'
        }
      }
    ]
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2021-03-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '172.27.${index}.0/24'
      ]
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '172.27.${index}.0/25'
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage.Global'
              locations: [
                '*'
              ]
            }
          ]
          networkSecurityGroup: {
            id: nsg.id
          }
        }
      }
    ]
  }
}

output subnetId string = vnet.properties.subnets[0].id
