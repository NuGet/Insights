using module "./ExplorePackages.psm1"
using namespace ExplorePackages

[CmdletBinding()]
[OutputType([Microsoft.Azure.Commands.ActiveDirectory.PSADApplication])]
param (
    [Parameter(Mandatory = $true)]
    [string]$AadAppName
)

Write-Status "Looking for AAD app with name '$AadAppName'..."
$existingApps = Get-AzADApplication -DisplayName $AadAppName

if ($existingApps.Count -eq 0) {
    Write-Status "Creating a new AAD app..."
    $app = New-AzADApplication `
        -DisplayName $AadAppName `
        -IdentifierUris "placeholder://$AadAppName"
    Write-Status "Created new app with object ID '$($app.ObjectId)'."
}
elseif ($existingApps.Count -eq 1) {
    $app = $existingApps[0]
    Write-Status "Using existing app with object ID '$($app.ObjectId)'."
}
else {
    throw "There are $($existingApps.Count) apps with the name '$AadAppName'."
}

$app
