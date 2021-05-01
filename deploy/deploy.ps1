using module "scripts/ExplorePackages.psm1"
using namespace ExplorePackages

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ConfigName,

    [Parameter(Mandatory = $false)]
    [string]$StampName,

    [Parameter(Mandatory = $false)]
    [switch]$AllowDeployUser,

    [Parameter(Mandatory = $false)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $false)]
    [string]$WorkerZipPath
)

$context = Get-AzContext
Write-Status "Using subscription: $($context.Subscription.Id)"

$configPath = Join-Path $PSScriptRoot "config/$ConfigName.json"
Write-Status "Using config path: $configPath"
$StampName = if (!$StampName) { $ConfigName } else { $StampName }
Write-Status "Using stamp name: $StampName"

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

# Publish (build and package) the app code
$deploymentDir = Join-Path $PSScriptRoot "../artifacts/deploy"
function Publish-Project ($ProjectName) {
    Write-Status "Publishing project '$ProjectName'..."
    dotnet publish (Join-Path $PSScriptRoot "../src/$ProjectName") `
        "/p:DeploymentDir=$deploymentDir" `
        --configuration Release | Out-Default
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish $ProjectName."
    }
    
    $path = Join-Path $deploymentDir "$ProjectName.zip"

    return $path.ToString()
}

if (!$WebsiteZipPath) { $WebsiteZipPath = Publish-Project "Website" }
if (!$WorkerZipPath) { $WorkerZipPath = Publish-Project "Worker" }

# Prepare the deployment parameters
$deployment = (Get-Config).Deployment
$parameters = @{
    ResourceSettings = [ResourceSettings]::new(
        $StampName,
        $deployment.Location,
        $deployment.WebsiteName,
        $deployment.ExistingWebsitePlanId,
        $WebsiteConfig,
        $deployment.WorkerSku,
        $deployment.WorkerCount,
        $deployment.WorkerLogLevel,
        $WorkerConfig,
        $null);
    DeploymentDir    = $deploymentDir;
    WebsiteZipPath   = $WebsiteZipPath;
    WorkerZipPath    = $WorkerZipPath;
}

Write-Status ""
Write-Status "Using the following deployment parameters:"
ConvertTo-Json $parameters -Depth 100 | Out-Default

. (Join-Path $PSScriptRoot "scripts/Invoke-Deploy.ps1") @parameters
