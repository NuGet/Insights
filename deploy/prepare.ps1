using module "scripts/ExplorePackages.psm1"
using namespace ExplorePackages

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ConfigName,

    [Parameter(Mandatory = $false)]
    [string]$StampName
)

$resourceSettings = Get-ResourceSettings $ConfigName $StampName

$parameters = @{ ResourceSettings = $resourceSettings }

Write-Status "Using the following deployment parameters:"
ConvertTo-Json $parameters -Depth 100 | Out-Default

Approve-SubscriptionId $resourceSettings.SubscriptionId

. (Join-Path $PSScriptRoot "scripts/Invoke-Prepare.ps1") @parameters | Out-Null
