param userManagedIdentityName string
param appInsightsName string

param deploymentLabel string
param customScriptExtensionFiles array

param location string
param vmssSku string
@minValue(0)
param minInstances int
@minValue(1)
param maxInstances int
param nsgName string
param vnetName string
param vmssName string
param nicName string
param ipConfigName string
param loadBalancerName string
param autoscaleName string
param adminUsername string
@secure()
param adminPassword string
param addLoadBalancer bool

resource ipConfig 'Microsoft.Network/publicIPAddresses@2022-05-01' = if (addLoadBalancer) {
  name: ipConfigName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    publicIPAddressVersion: 'IPv4'
  }
}

var frontendIPConfigurationName = '${loadBalancerName}-fipc'
var backendAddressPoolName = '${loadBalancerName}-bap'

resource loadBalancer 'Microsoft.Network/loadBalancers@2023-05-01' = if (addLoadBalancer) {
  name: loadBalancerName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    frontendIPConfigurations: [
      {
        name: frontendIPConfigurationName
        properties: {
          publicIPAddress: {
            id: ipConfig.id
          }
        }
      }
    ]
    backendAddressPools: [
      {
        name: backendAddressPoolName
        properties: {}
      }
    ]
    inboundNatRules: [
      {
        name: '${loadBalancerName}-rdp-inr'
        properties: {
          frontendIPConfiguration: {
            id: resourceId('Microsoft.Network/loadBalancers/frontendIPConfigurations', loadBalancerName, frontendIPConfigurationName)
          }
          backendAddressPool: {
            id: resourceId('Microsoft.Network/loadBalancers/backendAddressPools', loadBalancerName, backendAddressPoolName)
          }
          protocol: 'Tcp'
          backendPort: 3389
          frontendPortRangeStart: 50000
          frontendPortRangeEnd: 60000
        }
      }
    ]
    outboundRules: [
      {
        name: '${loadBalancerName}-or'
        properties: {
          frontendIPConfigurations: [
            {
              id: resourceId('Microsoft.Network/loadBalancers/frontendIPConfigurations', loadBalancerName, frontendIPConfigurationName)
            }
          ]
          backendAddressPool: {
            id: resourceId('Microsoft.Network/loadBalancers/backendAddressPools', loadBalancerName, backendAddressPoolName)
          }
          protocol: 'All'
        }
      }
    ]
  }
}

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
        '172.27.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '172.27.0.0/16'
          networkSecurityGroup: {
            id: nsg.id
          }
        }
      }
    ]
  }
}

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userManagedIdentityName
}

resource appInsights 'Microsoft.Insights/components@2015-05-01' existing = {
  name: appInsightsName
}

resource vmss 'Microsoft.Compute/virtualMachineScaleSets@2021-11-01' = {
  name: vmssName
  location: location
  sku: {
    name: vmssSku
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userManagedIdentity.id}': {}
    }
  }
  properties: {
    overprovision: false
    virtualMachineProfile: {
      storageProfile: {
        osDisk: {
          createOption: 'FromImage'
          diskSizeGB: skuInfo[vmssSku].diskSizeGB
          diffDiskSettings: {
            option: 'Local'
            placement: skuInfo[vmssSku].diffDiskPlacement
          }
        }
        imageReference: {
          publisher: 'MicrosoftWindowsServer'
          offer: 'WindowsServer'
          sku: '2022-datacenter-core-smalldisk'
          version: 'latest'
        }
      }
      networkProfile: {
        networkInterfaceConfigurations: [
          {
            name: nicName
            properties: {
              primary: true
              ipConfigurations: [
                {
                  name: ipConfigName
                  properties: {
                    subnet: {
                      id: vnet.properties.subnets[0].id
                    }
                    loadBalancerBackendAddressPools: addLoadBalancer ? [
                      {
                        id: loadBalancer.properties.backendAddressPools[0].id
                      }
                    ] : []
                  }
                }
              ]
            }
          }
        ]
      }
      priority: 'Spot'
      securityProfile: {
        encryptionAtHost: true
      }
      osProfile: {
        computerNamePrefix: 'insights'
        adminUsername: adminUsername
        adminPassword: adminPassword
      }
      extensionProfile: {
        extensionsTimeBudget: 'PT15M'
        extensions: [
          {
            name: 'InstallWorkerStandalone'
            properties: {
              publisher: 'Microsoft.Compute'
              type: 'CustomScriptExtension'
              typeHandlerVersion: '1.10'
              autoUpgradeMinorVersion: true
              settings: {
                fileUris: customScriptExtensionFiles
                commandToExecute: 'powershell -ExecutionPolicy Unrestricted -File "${deploymentLabel}/Install-WorkerStandalone.ps1" -DeploymentLabel "${deploymentLabel}" -HostPattern "AzureFunctionsHost.zip" -AppPattern "Worker.zip" -EnvPattern "WorkerStandalone.env" -LocalHealthPort 80 -ApplicationInsightsConnectionString "${appInsights.properties.ConnectionString}" -UserManagedIdentityClientId "${userManagedIdentity.properties.clientId}" -ExpandOSPartition'
              }
              protectedSettings: {
                managedIdentity: {
                  clientId: userManagedIdentity.properties.clientId
                }
              }
            }
          }
          {
            name: 'WorkerHealthProbe'
            properties: {
              provisionAfterExtensions: [
                'InstallWorkerStandalone'
              ]
              publisher: 'Microsoft.ManagedServices'
              type: 'ApplicationHealthWindows'
              typeHandlerVersion: '1.0'
              autoUpgradeMinorVersion: true
              enableAutomaticUpgrade: true
              settings: {
                protocol: 'http'
                port: 80
                requestPath: '/'
              }
            }
          }
        ]
      }
    }
    automaticRepairsPolicy: {
      enabled: true
      gracePeriod: 'PT15M'
    }
    upgradePolicy: {
      mode: 'Automatic'
    }
  }
}

var eventCounters = [
  'Workflow.StateTransition'
  'Timer.Execute'
  'CatalogScan.Update'
]
var eventCounterRules = [for event in eventCounters: {
  metricTrigger: {
    metricName: event
    metricNamespace: 'azure.applicationinsights'
    metricResourceUri: appInsights.id
    timeGrain: 'PT1M'
    statistic: 'Sum'
    timeWindow: 'PT20M'
    timeAggregation: 'Maximum'
    operator: 'GreaterThanOrEqual'
    threshold: 1
  }
  scaleAction: {
    direction: 'Increase'
    type: 'ExactCount'
    cooldown: 'PT1M'
    value: string(min(maxInstances, 5))
  }
}]

resource autoscale 'Microsoft.Insights/autoscalesettings@2015-04-01' = {
  name: autoscaleName
  location: location
  properties: {
    enabled: true
    targetResourceLocation: location
    targetResourceUri: vmss.id
    profiles: [
      {
        name: 'default'
        capacity: {
          default: string(minInstances)
          minimum: string(minInstances)
          maximum: string(maxInstances)
        }
        rules: concat(eventCounterRules, [
            {
              metricTrigger: {
                metricName: 'Percentage CPU'
                metricNamespace: 'microsoft.compute/virtualmachinescalesets'
                metricResourceUri: vmss.id
                timeGrain: 'PT1M'
                statistic: 'Average'
                timeWindow: 'PT10M'
                timeAggregation: 'Average'
                operator: 'GreaterThan'
                threshold: 25
              }
              scaleAction: {
                direction: 'Increase'
                type: 'ChangeCount'
                cooldown: 'PT1M'
                value: string(min(maxInstances - minInstances, 5))
              }
            }
            {
              metricTrigger: {
                metricName: 'Percentage CPU'
                metricNamespace: 'microsoft.compute/virtualmachinescalesets'
                metricResourceUri: vmss.id
                timeGrain: 'PT1M'
                statistic: 'Average'
                timeWindow: 'PT10M'
                timeAggregation: 'Average'
                operator: 'LessThan'
                threshold: 15
              }
              scaleAction: {
                direction: 'Decrease'
                type: 'ChangeCount'
                cooldown: 'PT2M'
                value: string(min(maxInstances - minInstances, 10))
              }
            }
          ])
      }
    ]
  }
}

// Calculated using this resource: https://github.com/joelverhagen/data-azure-spot-vms/blob/main/vm-skus.csv
// If a SKU has both a CacheDisk and a ResourceDisk with a capacity of a least 30 GB, the larger is selected.
var skuInfo = {
  Standard_D2a_v4: {
    diskSizeGB: 50
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D2ads_v5: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D2as_v4: {
    diskSizeGB: 50
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_D2d_v4: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D2d_v5: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D2ds_v4: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D2ds_v5: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D2s_v3: {
    diskSizeGB: 50
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_D4a_v4: {
    diskSizeGB: 100
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D4ads_v5: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D4as_v4: {
    diskSizeGB: 100
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_D4d_v4: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D4d_v5: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D4ds_v4: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D4ds_v5: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D4s_v3: {
    diskSizeGB: 100
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_D8a_v4: {
    diskSizeGB: 200
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D8ads_v5: {
    diskSizeGB: 300
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D8as_v4: {
    diskSizeGB: 200
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_D8d_v4: {
    diskSizeGB: 300
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D8d_v5: {
    diskSizeGB: 300
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D8ds_v4: {
    diskSizeGB: 300
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D8ds_v5: {
    diskSizeGB: 300
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_D8s_v3: {
    diskSizeGB: 200
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DC2ads_v5: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_DC2ds_v3: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_DC2s_v2: {
    diskSizeGB: 100
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_DC4ads_v5: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_DC4ds_v3: {
    diskSizeGB: 300
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_DC4s_v2: {
    diskSizeGB: 200
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_DC8_v2: {
    diskSizeGB: 400
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_DC8ads_v5: {
    diskSizeGB: 300
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_DS11_v2: {
    diskSizeGB: 72
    diffDiskPlacement: 'CacheDisk'
  }
  'Standard_DS11-1_v2': {
    diskSizeGB: 72
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DS11: {
    diskSizeGB: 72
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DS12_v2: {
    diskSizeGB: 144
    diffDiskPlacement: 'CacheDisk'
  }
  'Standard_DS12-1_v2': {
    diskSizeGB: 144
    diffDiskPlacement: 'CacheDisk'
  }
  'Standard_DS12-2_v2': {
    diskSizeGB: 144
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DS12: {
    diskSizeGB: 144
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DS2_v2: {
    diskSizeGB: 86
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DS2: {
    diskSizeGB: 86
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DS3_v2: {
    diskSizeGB: 172
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DS3: {
    diskSizeGB: 172
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DS4_v2: {
    diskSizeGB: 344
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_DS4: {
    diskSizeGB: 344
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_E2a_v4: {
    diskSizeGB: 50
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E2ads_v5: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E2as_v4: {
    diskSizeGB: 50
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_E2bds_v5: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E2d_v5: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E2ds_v4: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E2ds_v5: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E2s_v3: {
    diskSizeGB: 50
    diffDiskPlacement: 'CacheDisk'
  }
  'Standard_E4-2ads_v5': {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  'Standard_E4-2as_v4': {
    diskSizeGB: 99
    diffDiskPlacement: 'CacheDisk'
  }
  'Standard_E4-2ds_v4': {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  'Standard_E4-2ds_v5': {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  'Standard_E4-2s_v3': {
    diskSizeGB: 100
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_E4a_v4: {
    diskSizeGB: 100
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E4ads_v5: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E4as_v4: {
    diskSizeGB: 100
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_E4bds_v5: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E4d_v5: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E4ds_v4: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E4ds_v5: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_E4s_v3: {
    diskSizeGB: 100
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_EC2ads_v5: {
    diskSizeGB: 75
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_EC4ads_v5: {
    diskSizeGB: 150
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_F16s_v2: {
    diskSizeGB: 256
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_F16s: {
    diskSizeGB: 192
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_F2s_v2: {
    diskSizeGB: 32
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_F4s_v2: {
    diskSizeGB: 64
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_F4s: {
    diskSizeGB: 48
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_F8s_v2: {
    diskSizeGB: 128
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_F8s: {
    diskSizeGB: 96
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_GS1: {
    diskSizeGB: 264
    diffDiskPlacement: 'CacheDisk'
  }
  Standard_L4s: {
    diskSizeGB: 678
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_NC4as_T4_v3: {
    diskSizeGB: 176
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_NV4as_v4: {
    diskSizeGB: 88
    diffDiskPlacement: 'ResourceDisk'
  }
  Standard_NV8as_v4: {
    diskSizeGB: 176
    diffDiskPlacement: 'ResourceDisk'
  }
}
