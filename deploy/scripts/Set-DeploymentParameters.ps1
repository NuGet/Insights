[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$DeploymentLabel,

    [Parameter(Mandatory = $true)]
    [string]$ExpressV2Path
)

$parametersPattern = Join-Path (Resolve-Path $ExpressV2Path) "parameters/*.parameters.json"
Write-Host "Setting deployment parameters in $parametersPattern"

foreach ($path in Get-ChildItem $parametersPattern -Recurse) {
    Write-Host "Updating $path"
    $parameters = Get-Content $parametersPattern -Raw | ConvertFrom-Json
    if ($parameters.parameters.deploymentLabel) {
        Write-Host "  Setting deploymentLabel to '$DeploymentLabel'"
        $parameters.parameters.deploymentLabel.value = $DeploymentLabel
    }

    if ($parameters.parameters.spotWorkerAdminPassword -and $parameters.parameters.spotWorkerAdminPassword.value -eq "PLACEHOLDER") {
        Write-Host "  Setting spotWorkerAdminPassword to a random value"
        $random = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
        $buffer = New-Object byte[](32)
        $random.GetBytes($buffer)
        $password = "N1!" + [Convert]::ToBase64String($buffer)
        $parameters.parameters.spotWorkerAdminPassword.value = $password
    }

    $parameters | ConvertTo-Json -Depth 100 | Out-File $path -Encoding utf8
}
