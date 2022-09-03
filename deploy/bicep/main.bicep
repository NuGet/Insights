param deploymentLabel string
param storageAccountName string
param keyVaultName string
param leaseContainerName string
param userManagedIdentityName string
param location string

param appInsightsName string
param appInsightsDailyCapGb int
param actionGroupName string
param actionGroupShortName string
param alertEmail string
param alertPrefix string

param websitePlanId string = 'new'
param websitePlanName string = 'default'
param websiteName string
@secure()
param websiteZipUrl string
param websiteAadClientId string
param websiteConfig array

param workerPlanNamePrefix string
param workerPlanLocations array
@minValue(1)
param workerPlanCount int
@minValue(1)
param workerCountPerPlan int
param workerSku string = 'Y1'
param workerAutoscaleNamePrefix string
param workerMinInstances int
param workerMaxInstances int
param workerNamePrefix string
@secure()
param workerZipUrl string
param workerHostId string
param workerLogLevel string = 'Warning'
param workerConfig array

param useSpotWorkers bool
@secure()
param spotWorkerUploadScriptUrl string = ''
@secure()
param spotWorkerDeploymentUrls object = {}
param spotWorkerDeploymentContainerName string = ''
param spotWorkerAdminUsername string = ''
@secure()
param spotWorkerAdminPassword string = ''
param spotWorkerSpecs array = []

// Shared resources
resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' existing = {
  name: storageAccountName
}

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: userManagedIdentityName
  location: location
}

resource appInsights 'Microsoft.Insights/components@2015-05-01' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }

  // This produces a warning due to limited type definitions, but works.
  // See: https://github.com/Azure/bicep/issues/784#issuecomment-830997209
  #disable-next-line BCP081
  resource billing 'CurrentBillingFeatures' = {
    name: 'Basic'
    properties: {
      CurrentBillingFeatures: 'Basic'
      DataVolumeCap: {
        Cap: appInsightsDailyCapGb
        WarningThreshold: 90
      }
    }
  }
}

var sharedConfig = [
  {
    name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
    value: appInsights.properties.InstrumentationKey
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsights.properties.ConnectionString
  }
  {
    name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
    value: '~2'
  }
  {
    name: 'NuGet.Insights:DeploymentLabel'
    value: deploymentLabel
  }
  {
    name: 'NuGet.Insights:LeaseContainerName'
    value: leaseContainerName
  }
  {
    name: 'NuGet.Insights:StorageAccountName'
    value: storageAccountName
  }
  {
    name: 'NuGet.Insights:UserManagedIdentityClientId'
    value: userManagedIdentity.properties.clientId
  }
  {
    // See: https://github.com/projectkudu/kudu/wiki/Configurable-settings#ensure-update-site-and-update-siteconfig-to-take-effect-synchronously 
    name: 'WEBSITE_ENABLE_SYNC_UPDATE_SITE'
    value: '1'
  }
  {
    name: 'WEBSITE_RUN_FROM_PACKAGE'
    value: '1'
  }
]

// Storage and Key Vault
var storageAndKvLongName = '${deployment().name}-storage-and-kv'
var storageAndKvName = length(storageAndKvLongName) > 64 ? '${guid(deployment().name)}-storage-and-kv' : storageAndKvLongName
module storageAndKv './storage-and-kv.bicep' = {
  name: storageAndKvName
  params: {
    storageAccountName: storageAccountName
    keyVaultName: keyVaultName
    leaseContainerName: leaseContainerName
    location: location
  }
}

// Permissions
var permissionsDeploymentLongName = '${deployment().name}-permissions'
var permissionsDeploymentName = length(permissionsDeploymentLongName) > 64 ? '${guid(deployment().name)}-permissions' : permissionsDeploymentLongName

module permissions './permissions.bicep' = {
  name: permissionsDeploymentName
  params: {
    storageAccountName: storageAccountName
    keyVaultName: keyVaultName
    userManagedIdentityName: userManagedIdentityName
  }
  dependsOn: [
    storageAndKv
    userManagedIdentity
  ]
}

// Website
var websiteDeploymentLongName = '${deployment().name}-website'
var websiteDeploymentName = length(websiteDeploymentLongName) > 64 ? '${guid(deployment().name)}-website' : websiteDeploymentLongName

module website './website.bicep' = {
  name: websiteDeploymentName
  params: {
    userManagedIdentityName: userManagedIdentityName
    location: location
    planId: websitePlanId
    planName: websitePlanName
    name: websiteName
    zipUrl: websiteZipUrl
    aadClientId: websiteAadClientId
    config: concat(sharedConfig, websiteConfig)
  }
  dependsOn: [
    permissions
  ]
}

// Alerts
var alertsDeploymentLongName = '${deployment().name}-alerts'
var alertsDeploymentName = length(alertsDeploymentLongName) > 64 ? '${guid(deployment().name)}-alerts' : alertsDeploymentLongName

module alerts './alerts.bicep' = {
  name: alertsDeploymentName
  params: {
    storageAccountName: storageAccountName
    appInsightsName: appInsightsName
    websiteName: websiteName
    actionGroupName: actionGroupName
    alertEmail: alertEmail
    actionGroupShortName: actionGroupShortName
    alertPrefix: alertPrefix
  }
  dependsOn: [
    website
  ]
}

// Azure Functions workers
var workersDeploymentLongName = '${deployment().name}-workers'
var workersDeploymentName = length(workersDeploymentLongName) > 64 ? '${guid(deployment().name)}-workers' : workersDeploymentLongName

var disabledFunctionConfig = useSpotWorkers ? [
  {
    name: 'AzureWebJobs.ExpandQueueFunction.Disabled'
    value: 'true'
  }
  {
    name: 'AzureWebJobs.WorkQueueFunction.Disabled'
    value: 'true'
  }
] : []

module workers './function-workers.bicep' = {
  name: workersDeploymentName
  params: {
    storageAccountName: storageAccountName
    userManagedIdentityName: userManagedIdentityName
    planNamePrefix: workerPlanNamePrefix
    planLocations: workerPlanLocations
    planCount: workerPlanCount
    countPerPlan: workerCountPerPlan
    sku: workerSku
    autoscaleNamePrefix: workerAutoscaleNamePrefix
    minInstances: workerMinInstances
    maxInstances: workerMaxInstances
    namePrefix: workerNamePrefix
    zipUrl: workerZipUrl
    hostId: workerHostId
    logLevel: workerLogLevel
    config: concat(disabledFunctionConfig, sharedConfig, workerConfig)
  }
  dependsOn: [
    permissions
  ]
}

// Spot workers
var spotWorkersDeploymentLongName = '${deployment().name}-spot-workers'
var spotWorkersDeploymentName = length(spotWorkersDeploymentLongName) > 64 ? '${guid(deployment().name)}-spot-workers' : spotWorkersDeploymentLongName

module spotWorkers './spot-workers.bicep' = if (useSpotWorkers) {
  name: spotWorkersDeploymentName
  params: {
    location: location
    storageAccountName: storageAccountName
    userManagedIdentityName: userManagedIdentityName
    uploadScriptUrl: spotWorkerUploadScriptUrl
    deploymentUrls: spotWorkerDeploymentUrls
    deploymentLabel: deploymentLabel
    spotWorkerDeploymentContainerName: spotWorkerDeploymentContainerName
    appInsightsName: appInsightsName
    adminUsername: spotWorkerAdminUsername
    adminPassword: spotWorkerAdminPassword
    specs: spotWorkerSpecs
  }
  dependsOn: [
    permissions
  ]
}
