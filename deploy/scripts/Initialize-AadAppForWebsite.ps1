[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ObjectId,

    [Parameter(Mandatory = $false)]
    [string]$DefaultHostName,

    [Parameter(Mandatory = $false)]
    [string[]]$HostNames
)

. (Join-Path $PSScriptRoot "common.ps1")

Write-Status "Enabling the AAD app for website login..."
$resource = "https://graph.microsoft.com/"

$graphToken = Get-AzAccessToken -Resource $resource
$app = Invoke-RestMethod `
    -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/applications/$ObjectId" `
    -Headers @{ Authorization = "Bearer $($graphToken.Token)" }

$appServicePatch = @{
    api            = @{ requestedAccessTokenVersion = 2 };
    identifierUris = @();
    signInAudience = "AzureADandPersonalMicrosoftAccount";
    web            = @{
        homePageUrl  = "https://$($DefaultHostName)";
        redirectUris = @($HostNames | ForEach-Object { "https://$_/signin-oidc" })
        logoutUrl    = "https://$($DefaultHostName)/signout-oidc"
    }
}

if ($app.api.requestedAccessTokenVersion -eq $appServicePatch.api.requestedAccessTokenVersion) {
    $appServicePatch.Remove("api")
}
if (!$app.identifierUris) {
    $appServicePatch.Remove("identifierUris")
}
if ($app.signInAudience -eq $appServicePatch.signInAudience) {
    $appServicePatch.Remove("signInAudience")
}
if ($app.web.homePageUrl -eq $appServicePatch.web.homePageUrl) {
    $appServicePatch.web.Remove("homePageUrl")
}
if ($app.web.redirectUris -eq $appServicePatch.web.redirectUris) {
    $appServicePatch.web.Remove("redirectUris")
}
if ($app.web.logoutUrl -eq $appServicePatch.web.logoutUrl) {
    $appServicePatch.web.Remove("logoutUrl")
}
if ($appServicePatch.web.Count -eq 0) {
    $appServicePatch.Remove("web")
}

if ($appServicePatch.Count -eq 0) {
    Write-Host "The app is already ready for website login."
    exit
}

function Set-AadApp($token) {
    $graphHeaders = 
    Invoke-RestMethod `
        -Method PATCH `
        -Uri "https://graph.microsoft.com/v1.0/applications/$ObjectId" `
        -Headers @{ Authorization = "Bearer $token" } `
        -ContentType "application/json" `
        -Body ($appServicePatch | ConvertTo-Json -Depth 100 -Compress) `
        -ErrorAction Stop
}

try {
    Write-Status "Updating the AAD app using an Az PowerShell token."
    Set-AadApp($graphToken.Token)
}
catch {
    if ($_.Exception.Response.StatusCode -eq 403) {
        Write-Host "Failed with 403 (scopes are probably wrong)."
        Write-Status "Updating the AAD app using an Az CLI token."
        try {
            $graphToken = az account get-access-token --resource "https://graph.microsoft.com/" | ConvertFrom-Json
            if (!$graphToken -or $LASTEXITCODE -ne 0) { throw "Failed to get a token from Az CLI." }
            Set-AadApp($graphToken.accessToken)
            Write-Host "Succeeded."
        }
        catch {
            Write-Warning "Failed. You can work around this issue by setting these properties manually. The easiest place to do this is the Graph Explorer: https://developer.microsoft.com/en-us/graph/graph-explorer"
            Write-Host ""
            Write-Host "PATCH https://graph.microsoft.com/v1.0/applications/$ObjectId"
            Write-Host ""
            Write-Host ($appServicePatch | ConvertTo-Json -Depth 100)
            Write-Host ""
            throw
        }
    }
    else {
        throw
    }
}
