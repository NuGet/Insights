[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ConfigName,

    [Parameter(Mandatory = $false)]
    [string]$StackName,

    [Parameter(Mandatory = $false)]
    [switch]$AllowDeployUser
)

. (Join-Path $PSScriptRoot "scripts/common.ps1")

$context = Get-AzContext
Write-Status "Using subscription: $($context.Subscription.Id)"

$configPath = Join-Path $PSScriptRoot "config/$ConfigName.json"
Write-Status "Using config path: $configPath"
$StackName = if ($StackName) { $StackName } else { $ConfigName }
Write-Status "Using stack name: $StackName"

function Get-Config() { Get-Content $configPath | ConvertFrom-Json | ConvertTo-Hashtable }
function Get-AppConfig() { @{ "Knapcode.ExplorePackages" = @{} } }

# Prepare the website config
$websiteConfig = Get-Config
$websiteConfig = Merge-Hashtable (Get-AppConfig) $websiteConfig.AppSettings.Shared $websiteConfig.AppSettings.Website

# Optionally, add the current (deploying) user as an allowed user for the website admin panel.
if ($AllowDeployUser) {
    $context = Get-AzContext
    $homeAccountId = $context.Account.ExtendedProperties.HomeAccountId
    if (!$homeAccountId) {
        Write-Warning "Could not find the 'HomeAccountId' from (Get-AzContext).Account.ExtendedProperties.HomeAccountId"
    }
    else {
        if (!$websiteConfig['Knapcode.ExplorePackages'].AllowedUsers) {
            $websiteConfig['Knapcode.ExplorePackages'].AllowedUsers = @()
        }

        $objectId, $tenantId = $homeAccountId.Split('.', 2)
        Write-Status ""
        Write-Status "Adding allowed user:"
        Write-Status "  Tenant ID: $tenantId"
        Write-Status "  Object ID: $objectId"

        $websiteConfig['Knapcode.ExplorePackages'].AllowedUsers += @{
            TenantId = $tenantId;
            ObjectId = $objectId
        }
    }
}

# Prepare the worker config
$workerConfig = Get-Config
$workerConfig = Merge-Hashtable (Get-AppConfig) $workerConfig.AppSettings.Shared $workerConfig.AppSettings.Worker

# Prepare the deployment parameters
$parameters = Get-Config
$parameters = $parameters.Deployment
$parameters.StackName = $StackName
$parameters.WebsiteConfig = $websiteConfig
$parameters.WorkerConfig = $workerConfig

# Set up some default config based on worker SKU
if (!$parameters.WorkerSku) { $parameters.WorkerSku = "Y1" }
$npeEnabled = "NuGetPackageExplorerToCsv" -notin $workerConfig["Knapcode.ExplorePackages"].DisabledDrivers
if ($parameters.WorkerSku -eq "Y1") {
    if ($npeEnabled) {
        # Default "MoveTempToHome" to be true when NuGetPackageExplorerToCsv is enabled. We do this because the NuGet
        # Package Explorer symbol validation APIs are hard-coded to use TEMP and can quickly fill up the small TEMP
        # capacity on consumption plan (~500 MiB). Therefore, we move TEMP to HOME at the start of the process. HOME
        # points to a Azure Storage File share which has no capacity issues.
        if ($null -eq $workerConfig["Knapcode.ExplorePackages"].MoveTempToHome) {
            $workerConfig["Knapcode.ExplorePackages"].MoveTempToHome = $true
        }

        # Default the maximum number of workers per Function App plan to 16 when NuGetPackageExplorerToCsv is enabled.
        # We do this because it's easy for a lot of Function App workers to overload the HOME directory which is backed
        # by an Azure Storage File share.
        if ($null -eq $workerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT) {
            $workerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT = 16
        }

        # Default the storage queue trigger batch size to 1 when NuGetPackageExplorerToCsv is enabled. We do this to
        # eliminate the parallelism in the worker process so that we can easily control the number of total parallel
        # queue messages are being processed and therefore are using the HOME file share.
        if ($null -eq $workerConfig.AzureFunctionsJobHost__extensions__queues__batchSize) {
            $workerConfig.AzureFunctionsJobHost__extensions__queues__batchSize = 1
        }
    }
}

Write-Status ""
Write-Status "Using the following deployment parameters:"
ConvertTo-Json $parameters -Depth 100 | Out-Default

Write-Status ""
Write-Status "Beginning the deployment process..."

. (Join-Path $PSScriptRoot "scripts/Invoke-Deploy") @parameters -ErrorAction Stop
