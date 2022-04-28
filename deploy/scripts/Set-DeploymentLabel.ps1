[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$DeploymentLabel,

    [Parameter(Mandatory = $true)]
    [string]$ExpressV2Path
)

$parametersPattern = Join-Path (Resolve-Path $ExpressV2Path) "parameters/*.parameters.json"
Write-Host "Setting deployment label '$DeploymentLabel' in $parametersPattern"

foreach ($path in Get-ChildItem $parametersPattern -Recurse) {
    Write-Host "Updating $path"
    $parameters = Get-Content $parametersPattern -Raw | ConvertFrom-Json
    $parameters.parameters.deploymentLabel.value = $DeploymentLabel
    $parameters | ConvertTo-Json -Depth 100 | Out-File $path -Encoding utf8
}
