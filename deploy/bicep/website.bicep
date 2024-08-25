param userManagedIdentityName string

param location string

param planId string = 'new'
param planName string = 'default'
param isLinux bool
param runFromZipUrl bool

param name string
@secure()
param zipUrl string
param config object
param subnetId string

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userManagedIdentityName
}

// Website
resource websitePlan 'Microsoft.Web/serverfarms@2022-09-01' = if (planId == 'new') {
  name: planName == 'default' ? '${name}-WebsitePlan' : planName
  location: location
  kind: isLinux ? 'linux' : 'windows'
  sku: {
    name: 'B1'
  }
  properties: {
    reserved: isLinux
  }
}

var appSettings = [
  for item in items(config): {
    name: item.key
    value: item.value
  }
]

resource website 'Microsoft.Web/sites@2022-09-01' = {
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
    virtualNetworkSubnetId: subnetId
    siteConfig: union(
      {
        minTlsVersion: '1.2'
        alwaysOn: false // I've run into problems with the AAD client certificate not refreshing...
        use32BitWorkerProcess: false
        healthCheckPath: '/healthz'
        appSettings: appSettings
      },
      isLinux
        ? {
            linuxFxVersion: 'DOTNETCORE|8.0'
          }
        : {
            netFrameworkVersion: 'v8.0'
          }
    )
  }

  resource websiteDeploy 'extensions' = if (!runFromZipUrl) {
    name: any('ZipDeploy') // Workaround per: https://github.com/Azure/bicep/issues/784#issuecomment-817260643
    properties: {
      packageUri: zipUrl
    }
  }
}
