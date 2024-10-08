[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$AadAppName,

    [Parameter(Mandatory = $false)]
    [string]$ServiceManagementReference
)

Import-Module (Join-Path $PSScriptRoot "NuGet.Insights.psm1")

Write-Status "Looking for AAD app with name '$AadAppName'..."
$existingApps = @(Get-AzADApplication -DisplayName $AadAppName)

if ($existingApps.Count -eq 0) {
    Write-Status "Creating a new AAD app..."
    $app = New-AzADApplication `
        -DisplayName $AadAppName
    Write-Status "Created new app with object ID '$($app.id)'."
}
elseif ($existingApps.Count -eq 1) {
    $app = $existingApps[0]
    Write-Status "Using existing app with object ID '$($app.id)'."
}
else {
    throw "There are $($existingApps.Count) apps with the name '$AadAppName'."
}

if ($ServiceManagementReference -and $app.ServiceManagementReference -ne $ServiceManagementReference) {
    Write-Status "Updating AAD app to have service management reference '$ServiceManagementReference'."
    Update-AzADApplication -ObjectId $app.Id -ServiceManagementReference $ServiceManagementReference
}

$app
