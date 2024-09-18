param deploymentLabel string
param storageAccountName string
param keyVaultName string
param deploymentContainerName string
param leaseContainerName string
param userManagedIdentityName string
param location string

param logAnalyticsWorkspaceName string
param appInsightsName string
param appInsightsDailyCapGb int
param actionGroupName string
param actionGroupShortName string
param alertEmail string
param alertPrefix string

param allowedIpRanges array

param websitePlanId string = 'new'
param websitePlanName string = 'default'
param websiteIsLinux bool
param websiteName string
@secure()
param websiteZipUrl string
param websiteConfig object
param websiteLocation string

param workerPlanNamePrefix string
param workerPlanLocations array
@minValue(1)
param workerPlanCount int
@minValue(1)
param workerCountPerPlan int
param workerSku string
param workerIsLinux bool
param workerAutoscaleNamePrefix string
param workerMinInstances int
param workerMaxInstances int
param workerNamePrefix string
@secure()
param workerZipUrl string
param workerConfig object

param useSpotWorkers bool
param spotWorkerAdminUsername string = ''
@secure()
param spotWorkerAdminPassword string = ''
param spotWorkerSpecs array = []
param spotWorkerCustomScriptExtensionFiles array = [] // an array of blob URLs, must be accessible with the managed identity
param spotWorkerImageReference object = {}
param spotWorkerEnableAutomaticOSUpgrade bool = false

// Shared resources
resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: userManagedIdentityName
  location: location
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    features: {
      immediatePurgeDataOn30Days: true
    }
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: appInsightsDailyCapGb
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// Storage
var storageLongName = '${deployment().name}-storage'
var storageName = length(storageLongName) > 64 ? '${guid(deployment().name)}-storage' : storageLongName

module storage './storage.bicep' = {
  name: storageName
  params: {
    storageAccountName: storageAccountName
    deploymentContainerName: deploymentContainerName
    leaseContainerName: leaseContainerName
    location: location
    denyTraffic: !workerIsConsumptionPlan
    allowSharedKeyAccess: workerIsConsumptionPlan
  }
}

// KeyVault
var kvDeploymentLongName = '${deployment().name}-kv'
var kvDeploymentName = length(kvDeploymentLongName) > 64 ? '${guid(deployment().name)}-kv' : kvDeploymentLongName

module keyVault './key-vault.bicep' = {
  name: kvDeploymentName
  params: {
    keyVaultName: keyVaultName
    location: location
    workspaceId: logAnalyticsWorkspace.id
  }
}

// Permissions
var permissionsDeploymentLongName = '${deployment().name}-permissions'
var permissionsDeploymentName = length(permissionsDeploymentLongName) > 64
  ? '${guid(deployment().name)}-permissions'
  : permissionsDeploymentLongName

module permissions './permissions.bicep' = {
  name: permissionsDeploymentName
  params: {
    storageAccountName: storageAccountName
    keyVaultName: keyVaultName
    userManagedIdentityName: userManagedIdentityName
  }
  dependsOn: [
    storage
    keyVault
    userManagedIdentity
  ]
}

// Vnets
var vnetsDeploymentLongName = '${deployment().name}-vnets'
var vnetsDeploymentName = length(vnetsDeploymentLongName) > 64
  ? '${guid(deployment().name)}-vnets'
  : vnetsDeploymentLongName

module vnets './vnets.bicep' = {
  name: vnetsDeploymentName
  params: {
    spotWorkerSpecs: spotWorkerSpecs
    useSpotWorkers: useSpotWorkers
    websiteName: websiteName
    websiteLocation: websiteLocation
    workerCountPerPlan: workerCountPerPlan
    workerNamePrefix: workerNamePrefix
    workerPlanCount: workerPlanCount
    workerPlanLocations: workerPlanLocations
  }
}

// Update storage network ACLs (e.g. allowed subnets)
var storageNetworkAclsLongName = '${deployment().name}-storage-network-acls'
var storageNetworkAclsName = length(storageNetworkAclsLongName) > 64
  ? '${guid(deployment().name)}-storage-network-acls'
  : storageNetworkAclsLongName

module storageNetworkAcls './storage-network-acls.bicep' = {
  name: storageNetworkAclsName
  params: {
    storageAccountName: storageAccountName
    location: location
    denyTraffic: !workerIsConsumptionPlan
    subnetIds: concat([vnets.outputs.websiteSubnetId], vnets.outputs.workerSubnetIds, vnets.outputs.spotWorkerSubnetIds)
    allowedIpRanges: allowedIpRanges
  }
}

var sharedConfig = {
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
  ApplicationInsightsAgent_EXTENSION_VERSION: '~2'
  NuGetInsights__DeploymentLabel: deploymentLabel
  NuGetInsights__UserManagedIdentityClientId: userManagedIdentity.properties.clientId
  NUGET_INSIGHTS_ALLOW_ICU: 'true'
}

// Website
var websiteDeploymentLongName = '${deployment().name}-website'
var websiteDeploymentName = length(websiteDeploymentLongName) > 64
  ? '${guid(deployment().name)}-website'
  : websiteDeploymentLongName

// I've see weird deployment timeouts or "Central directory corrupt" errors when using ZipDeploy on Linux.
var websiteRunFromZipUrl = websiteIsLinux

var finalWebsiteConfig = union(
  websiteConfig,
  sharedConfig,
  {
    AzureAd__ClientCredentials__0__ManagedIdentityClientId: userManagedIdentity.properties.clientId
    // See: https://github.com/projectkudu/kudu/wiki/Configurable-settings#ensure-update-site-and-update-siteconfig-to-take-effect-synchronously 
    WEBSITE_ENABLE_SYNC_UPDATE_SITE: '1'
    WEBSITE_RUN_FROM_PACKAGE: websiteRunFromZipUrl ? split(websiteZipUrl, '?')[0] : '1'
  },
  websiteRunFromZipUrl ? { WEBSITE_RUN_FROM_PACKAGE_BLOB_MI_RESOURCE_ID: userManagedIdentity.id } : {}
)

module website './website.bicep' = {
  name: websiteDeploymentName
  params: {
    userManagedIdentityName: userManagedIdentityName
    location: websiteLocation
    planId: websitePlanId
    planName: websitePlanName
    isLinux: websiteIsLinux
    runFromZipUrl: websiteRunFromZipUrl
    name: websiteName
    zipUrl: websiteZipUrl
    config: finalWebsiteConfig
    subnetId: vnets.outputs.websiteSubnetId
  }
  dependsOn: [
    permissions
    storageNetworkAcls
  ]
}

// Alerts
var alertsDeploymentLongName = '${deployment().name}-alerts'
var alertsDeploymentName = length(alertsDeploymentLongName) > 64
  ? '${guid(deployment().name)}-alerts'
  : alertsDeploymentLongName

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
var workersDeploymentName = length(workersDeploymentLongName) > 64
  ? '${guid(deployment().name)}-workers'
  : workersDeploymentLongName

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' existing = {
  name: storageAccountName
}

var sakConnectionString = 'AccountName=${storageAccountName};AccountKey=${storageAccount.listkeys().keys[0].value};DefaultEndpointsProtocol=https;EndpointSuffix=${environment().suffixes.storage}'
var workerIsConsumptionPlan = workerSku == 'Y1'

// See: https://learn.microsoft.com/en-us/azure/azure-functions/run-functions-from-deployment-package#using-website_run_from_package--url
// Also, I've see weird deployment timeouts or "Central directory corrupt" errors when using ZipDeploy on Linux.
var workerRunFromZipUrl = workerIsLinux

var finalWorkerConfig = union(
  workerConfig,
  sharedConfig,
  useSpotWorkers
    ? {
        'AzureWebJobs.ExpandQueueFunction.Disabled': 'true'
        'AzureWebJobs.WorkQueueFunction.Disabled': 'true'
      }
    : {},
  workerIsConsumptionPlan
    ? {
        WEBSITE_CONTENTAZUREFILECONNECTIONSTRING: sakConnectionString
      }
    : {},
  {
    AzureWebJobsStorage__credential: 'managedidentity'
    AzureWebJobsStorage__clientId: userManagedIdentity.properties.clientId
    FUNCTIONS_EXTENSION_VERSION: '~4'
    WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED: '1'
    SCM_DO_BUILD_DURING_DEPLOYMENT: 'false'
    QueueTriggerConnection__credential: 'managedidentity'
    QueueTriggerConnection__clientId: userManagedIdentity.properties.clientId
    // See: https://github.com/projectkudu/kudu/wiki/Configurable-settings#ensure-update-site-and-update-siteconfig-to-take-effect-synchronously 
    WEBSITE_ENABLE_SYNC_UPDATE_SITE: '1'
    WEBSITE_RUN_FROM_PACKAGE: workerRunFromZipUrl ? split(workerZipUrl, '?')[0] : '1'
  },
  workerRunFromZipUrl ? { WEBSITE_RUN_FROM_PACKAGE_BLOB_MI_RESOURCE_ID: userManagedIdentity.id } : {}
)

module workers './function-workers.bicep' = {
  name: workersDeploymentName
  params: {
    userManagedIdentityName: userManagedIdentityName
    planNamePrefix: workerPlanNamePrefix
    planLocations: workerPlanLocations
    planCount: workerPlanCount
    countPerPlan: workerCountPerPlan
    sku: workerSku
    isLinux: workerIsLinux
    autoscaleNamePrefix: workerAutoscaleNamePrefix
    minInstances: workerMinInstances
    maxInstances: workerMaxInstances
    namePrefix: workerNamePrefix
    zipUrl: workerZipUrl
    config: finalWorkerConfig
    subnetIds: vnets.outputs.workerSubnetIds
    runFromZipUrl: workerRunFromZipUrl
    isConsumptionPlan: workerIsConsumptionPlan
  }
  dependsOn: [
    permissions
    storageNetworkAcls
  ]
}

// Spot workers
var spotWorkersDeploymentLongName = '${deployment().name}-spot-workers'
var spotWorkersDeploymentName = length(spotWorkersDeploymentLongName) > 64
  ? '${guid(deployment().name)}-spot-workers'
  : spotWorkersDeploymentLongName

module spotWorkers './spot-workers.bicep' = if (useSpotWorkers) {
  name: spotWorkersDeploymentName
  params: {
    keyVaultName: keyVaultName
    userManagedIdentityName: userManagedIdentityName
    storageAccountName: storageAccountName
    deploymentContainerName: deploymentContainerName
    customScriptExtensionFiles: spotWorkerCustomScriptExtensionFiles
    deploymentLabel: deploymentLabel
    appInsightsName: appInsightsName
    adminUsername: spotWorkerAdminUsername
    adminPassword: spotWorkerAdminPassword
    specs: spotWorkerSpecs
    subnetIds: vnets.outputs.spotWorkerSubnetIds
    imageReference: spotWorkerImageReference
    enableAutomaticOSUpgrade: spotWorkerEnableAutomaticOSUpgrade
  }
  dependsOn: [
    permissions
    storageNetworkAcls
  ]
}
