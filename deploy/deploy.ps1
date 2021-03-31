[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ConfigName,

    [Parameter(Mandatory)]
    [string]$StackName
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "scripts/common.ps1")

$configPath = Join-Path $PSScriptRoot "config/$ConfigName.json"
function Get-Config() { Get-Content $configPath | ConvertFrom-Json | ConvertTo-Hashtable }

# Prepare the website config
$websiteConfig = Get-Config
$websiteConfig = Merge-Hashtable $websiteConfig.AppSettings.Shared $websiteConfig.AppSettings.Website

# Prepare the worker config
$workerConfig = Get-Config
$workerConfig = Merge-Hashtable $workerConfig.AppSettings.Shared $workerConfig.AppSettings.Worker

# Prepare the deployment parameters
$parameters = Get-Config
$parameters = $parameters.Deployment
$parameters.StackName = if ($StackName) { $StackName } else {$ConfigName }
$parameters.WebsiteConfig = $websiteConfig
$parameters.WorkerConfig = $workerConfig

. (Join-Path $PSScriptRoot "scripts/Invoke-Deploy") @parameters
