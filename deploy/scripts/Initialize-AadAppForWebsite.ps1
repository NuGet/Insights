[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ObjectId,

    [Parameter(Mandatory = $true)]
    [string]$BaseUrl
)

Import-Module (Join-Path $PSScriptRoot "NuGet.Insights.psm1")

Write-Status "Enabling the AAD app for website login..."
$resource = "https://graph.microsoft.com/"
$graphToken = Get-ResourceAccessToken $resource
$attempt = 0;
$maxRetries = 10
while ($true) {
    try {
        $attempt++
        $app = Invoke-RestMethod `
            -Method GET `
            -Uri "https://graph.microsoft.com/v1.0/applications/$ObjectId" `
            -Headers @{ Authorization = "Bearer $graphToken" } `
            -ErrorAction Stop
        break
    }
    catch {
        if ($attempt -lt $maxRetries -and $_.Exception.Response.StatusCode -eq 404) {
            Write-Warning "Attempt $($attempt) - HTTP 404 Not Found. Trying again in 10 seconds."
            Start-Sleep 10
            continue
        } 
        throw
    }
}


$appServicePatch = @{
    api            = @{ requestedAccessTokenVersion = 2 };
    identifierUris = @();
    signInAudience = "AzureADandPersonalMicrosoftAccount";
    web            = @{
        homePageUrl           = $BaseUrl;
        implicitGrantSettings = @{
            enableIdTokenIssuance     = $true;
        };
        redirectUris          = @("$BaseUrl/signin-oidc");
        logoutUrl             = "$BaseUrl/signout-oidc";
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
if ($app.web.implicitGrantSettings.enableIdTokenIssuance -eq $appServicePatch.web.implicitGrantSettings.enableIdTokenIssuance) {
    $appServicePatch.web.implicitGrantSettings.Remove("enableIdTokenIssuance")
}
if ($appServicePatch.web.implicitGrantSettings.Count -eq 0) {
    $appServicePatch.web.Remove("implicitGrantSettings")
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
    exit
}

function Set-AadApp($token) {
    Invoke-RestMethod `
        -Method PATCH `
        -Uri "https://graph.microsoft.com/v1.0/applications/$ObjectId" `
        -Headers @{ Authorization = "Bearer $token" } `
        -ContentType "application/json" `
        -Body ($appServicePatch | ConvertTo-Json -Depth 100 -Compress) `
        -ErrorAction Stop | Out-Default
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
            $graphToken = az account get-access-token --resource $resource | ConvertFrom-Json
            if (!$graphToken -or $LASTEXITCODE -ne 0) { throw "Failed to get a token from Az CLI." }
            Set-AadApp($graphToken.accessToken)
            Write-Host "Succeeded."
        }
        catch {
            Write-Warning "Failed. You can work around this issue by setting these properties manually. The easiest place to do this is the Graph Explorer: https://developer.microsoft.com/en-us/graph/graph-explorer"
            Write-Host ""
            Write-Host "PATCH https://graph.microsoft.com/v1.0/applications/$ObjectId"
            Write-Host ""
            Write-Host ($appServicePatch | ConvertTo-Json -Depth 100 | Format-Json)
            Write-Host ""
            throw
        }
    }
    else {
        throw
    }
}
