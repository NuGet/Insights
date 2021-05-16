[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ConfigName,

    [Parameter(Mandatory = $false)]
    [string]$StampName
)

Import-Module (Join-Path $PSScriptRoot "scripts/NuGet.Insights.psm1")

$resourceSettings = Get-ResourceSettings $ConfigName $StampName

$parameters = @{ ResourceSettings = $resourceSettings }

Write-Status "Using the following deployment parameters:"
ConvertTo-Json $parameters -Depth 100 | Out-Default

Approve-SubscriptionId $resourceSettings.SubscriptionId

. (Join-Path $PSScriptRoot "scripts/Invoke-Prepare.ps1") @parameters | Out-Null
