param userManagedIdentityName string

param location string

param planId string = 'new'
param planName string = 'default'

param name string
@secure()
param zipUrl string
param aadClientId string
param config array

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userManagedIdentityName
}

// Website
resource websitePlan 'Microsoft.Web/serverfarms@2020-09-01' = if (planId == 'new') {
  name: planName == 'default' ? '${name}-WebsitePlan' : planName
  location: location
  sku: {
    name: 'B1'
  }
}

resource website 'Microsoft.Web/sites@2020-09-01' = {
  name: name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userManagedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: planId == 'new' ? websitePlan.id : planId
    clientAffinityEnabled: false
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      alwaysOn: true
      use32BitWorkerProcess: false
      netFrameworkVersion: 'v6.0'
      appSettings: concat([
        {
          name: 'AzureAd:Instance'
          value: environment().authentication.loginEndpoint
        }
        {
          name: 'AzureAd:ClientId'
          value: aadClientId
        }
        {
          name: 'AzureAd:TenantId'
          value: 'common'
        }
        {
          name: 'AzureAd:UserAssignedManagedIdentityClientId'
          value: userManagedIdentity.properties.clientId
        }
      ], config)
    }
  }

  resource deploy 'extensions' = {
    name: any('ZipDeploy') // Workaround per: https://github.com/Azure/bicep/issues/784#issuecomment-817260643
    properties: {
      packageUri: zipUrl
    }
  }
}
