[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$DeploymentLabel,

    [Parameter(Mandatory = $true)]
    [string]$ExpressV2Path
)

Import-Module (Join-Path $PSScriptRoot "NuGet.Insights.psm1")

$parametersPattern = Join-Path (Resolve-Path $ExpressV2Path) "parameters/*.parameters.json"
Write-Host "Setting deployment parameters in $parametersPattern"

foreach ($path in Get-ChildItem $parametersPattern -Recurse) {
    Write-Host "Updating $path"
    $parameters = Get-Content $path -Raw | ConvertFrom-Json
    if ($parameters.parameters.deploymentLabel) {
        Write-Host "  Setting deploymentLabel to '$DeploymentLabel'"
        $parameters.parameters.deploymentLabel.value = $DeploymentLabel
    }

    if ($parameters.parameters.spotWorkerAdminPassword -and $parameters.parameters.spotWorkerAdminPassword.value -eq "") {
        Write-Host "  Setting spotWorkerAdminPassword to a random value"
        $parameters.parameters.spotWorkerAdminPassword.value = Get-RandomPassword
    }

    $parameters | ConvertTo-Json -Depth 100 | Out-File $path -Encoding utf8
}
