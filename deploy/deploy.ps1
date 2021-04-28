[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ConfigName,

    [Parameter(Mandatory = $false)]
    [string]$StackName,

    [Parameter(Mandatory = $false)]
    [switch]$AllowDeployUser,

    [Parameter(Mandatory = $false)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $false)]
    [string]$WorkerZipPath
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
function Publish-Project ($ProjectName) {
    Write-Status "Publishing project '$ProjectName'..."
    dotnet publish (Join-Path $PSScriptRoot "../src/$ProjectName") `
        "/p:DeploymentDir=$DeploymentDir" `
        --configuration Release | Out-Default
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish $ProjectName."
    }
    
    $path = Join-Path $DeploymentDir "$ProjectName.zip"

    return $path.ToString()
}

if (!$WebsiteZipPath) { $WebsiteZipPath = Publish-Project "Website" }
if (!$WorkerZipPath) { $WorkerZipPath = Publish-Project "Worker" }

# Prepare the deployment parameters
$parameters = Merge-Hashtable (Get-Config).Deployment
$parameters.DeploymentDir = $deploymentDir
$parameters.StackName = $StackName
$parameters.WebsiteZipPath = $WebsiteZipPath
$parameters.WorkerZipPath = $WorkerZipPath
$parameters.WebsiteConfig = $websiteConfig
$parameters.WorkerConfig = $workerConfig

Write-Status ""
Write-Status "Using the following deployment parameters:"
ConvertTo-Json $parameters -Depth 100 | Out-Default

. (Join-Path $PSScriptRoot "scripts/Invoke-Deploy.ps1") @parameters -ErrorAction Stop
