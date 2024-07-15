param location string
param name string
param index int

var nsgName = '${name}-nsg'
var vnetName = '${name}-vnet'

resource nsg 'Microsoft.Network/networkSecurityGroups@2021-03-01' = {
  name: nsgName
  location: location
  properties: {
    securityRules: []
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2021-03-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '172.26.${index}.0/24'
      ]
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '172.26.${index}.0/25'
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage.Global'
              locations: [
                '*'
              ]
            }
          ]
          delegations: [
            {
              name: 'Microsoft.Web/serverfarms'
              properties: {
                serviceName: 'Microsoft.Web/serverfarms'
              }
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
