[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$AadAppName
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

Write-Status "Looking for AAD app with name '$AadAppName'..."
Invoke-Call { az ad app list `
    --display-name $AadAppName `
    --query "[].{displayName: displayName, appId: appId, objectId: objectId}" } | Tee-Object -Variable 'existingApps' | Out-Host
$existingApps = $existingApps | ConvertFrom-Json
    
if ($existingApps.Count -eq 0) {
    Write-Status "Creating a new AAD app..."
    Invoke-Call { az ad app create `
        --display-name $AadAppName `
        --query "{displayName: displayName, appId: appId, objectId: objectId}" } | Tee-Object -Variable 'app' | Out-Host
    $app = $app | ConvertFrom-Json
    Write-Status "Created new app with object ID '$($app.objectId)'."
} elseif ($existingApps.Count -eq 1) {
    $app = $existingApps[0]
    Write-Status "Using existing app with object ID '$($app.objectId)'."
} else {
    Write-Warning "There are $($existingApps.Count) apps with the name '$AadAppName'. Using the first with object ID '$($app.objectId)'."
    $app = $existingApps[0]
}

New-Object PSObject -Property ([ordered]@{
    appId = $app.appId;
    objectId = $app.objectId
})
