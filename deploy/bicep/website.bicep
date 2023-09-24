param userManagedIdentityName string

param location string

param planId string = 'new'
param planName string = 'default'
param isLinux bool

param name string
@secure()
param zipUrl string
param aadClientId string
param config array

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: userManagedIdentityName
}

// I've see weird deployment timeouts or "Central directory corrupt" errors when using ZipDeploy on Linux.
var runFromZipUrl = isLinux

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
    siteConfig: union({
        minTlsVersion: '1.2'
        alwaysOn: false // I've run into problems with the AAD client certificate not refreshing...
        use32BitWorkerProcess: false
        appSettings: concat([
            {
              name: 'AzureAd__Instance'
              value: environment().authentication.loginEndpoint
            }
            {
              name: 'AzureAd__ClientId'
              value: aadClientId
            }
            {
              name: 'AzureAd__TenantId'
              value: 'common'
            }
            {
              name: 'AzureAd__ClientCredentials__0__ManagedIdentityClientId'
              value: userManagedIdentity.properties.clientId
            }
            {
              // See: https://github.com/projectkudu/kudu/wiki/Configurable-settings#ensure-update-site-and-update-siteconfig-to-take-effect-synchronously 
              name: 'WEBSITE_ENABLE_SYNC_UPDATE_SITE'
              value: '1'
            }
            {
              name: 'WEBSITE_RUN_FROM_PACKAGE'
              value: runFromZipUrl ? split(zipUrl, '?')[0] : '1'
            }
          ], runFromZipUrl ? [
            {
              name: 'WEBSITE_RUN_FROM_PACKAGE_BLOB_MI_RESOURCE_ID'
              value: userManagedIdentity.id
            }
          ] : [], config)
      }, isLinux ? {
        linuxFxVersion: 'DOTNETCORE|7.0'
      } : {
        netFrameworkVersion: 'v7.0'
      })
  }

  resource websiteDeploy 'extensions' = if (!runFromZipUrl) {
    name: any('ZipDeploy') // Workaround per: https://github.com/Azure/bicep/issues/784#issuecomment-817260643
    properties: {
      packageUri: zipUrl
    }
  }
}
