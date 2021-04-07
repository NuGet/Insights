[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ConfigName,

    [Parameter(Mandatory = $false)]
    [string]$StackName,

    [Parameter(Mandatory = $false)]
    [switch]$AllowDeployUser
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "scripts/common.ps1")

$configPath = Join-Path $PSScriptRoot "config/$ConfigName.json"
Write-Status "Using config path: $configPath"
$StackName = if ($StackName) { $StackName } else { $ConfigName }
Write-Status "Using stack name: $StackName"

function Get-Config() { Get-Content $configPath | ConvertFrom-Json | ConvertTo-Hashtable }

# Prepare the website config
$websiteConfig = Get-Config
$websiteConfig = Merge-Hashtable $websiteConfig.AppSettings.Shared $websiteConfig.AppSettings.Website

# Optionally, add the current (deploying) user as an allowed user for the website admin panel.
function Get-SaltedHash($value) {
    $salt = "Knapcode.ExplorePackages-8DHU4R9URVLNHTQC2SS21ATB95U1VD1J-"
    $path = New-TemporaryFile
    "$salt$value" | Out-File -FilePath $path -Encoding UTF8 -NoNewline
    $hash = Get-FileHash -Path $path -Algorithm SHA256
    Remove-Item $path
    return $hash.Hash.ToLowerInvariant()
}

if ($AllowDeployUser) {
    $context = Get-AzContext
    $homeAccountId = $context.Account.ExtendedProperties.HomeAccountId
    if (!$homeAccountId) {
        Write-Warning "Could not find the 'HomeAccountId' from (Get-AzContext).Account.ExtendedProperties.HomeAccountId"
    } else {
        $objectId, $tenantId = $homeAccountId.Split('.', 2)
        if (!$websiteConfig['Knapcode.ExplorePackages']) {
            $websiteConfig['Knapcode.ExplorePackages'] = @{}
        }

        if (!$websiteConfig['Knapcode.ExplorePackages'].AllowedUsers) {
            $websiteConfig['Knapcode.ExplorePackages'].AllowedUsers = @()
        }

        $hashedTenantId = Get-SaltedHash $tenantId
        $hashedObjectId = Get-SaltedHash $objectId

        Write-Status "Adding allowed user:"
        Write-Status "  Tenant ID: $tenantId (hashed $hashedTenantId)"
        Write-Status "  Object ID: $objectId (hashed $hashedObjectId)"

        $websiteConfig['Knapcode.ExplorePackages'].AllowedUsers += @{
            HashedTenantId = Get-SaltedHash $tenantId "tid.txt";
            HashedObjectId = Get-SaltedHash $objectId "oid.txt"
        }
    }
}

# Prepare the worker config
$workerConfig = Get-Config
$workerConfig = Merge-Hashtable $workerConfig.AppSettings.Shared $workerConfig.AppSettings.Worker

# Prepare the deployment parameters
$parameters = Get-Config
$parameters = $parameters.Deployment
$parameters.StackName = $StackName
$parameters.WebsiteConfig = $websiteConfig
$parameters.WorkerConfig = $workerConfig

Write-Status ""
Write-Status "Beginning the deployment process..."

. (Join-Path $PSScriptRoot "scripts/Invoke-Deploy") @parameters
