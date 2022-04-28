[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ConfigName,

    [Parameter(Mandatory = $true)]
    [string]$RuntimeIdentifier,

    [Parameter(Mandatory = $false)]
    [string]$StampName,

    [Parameter(Mandatory = $false)]
    [switch]$AllowDeployUser,

    [Parameter(Mandatory = $false)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $false)]
    [string]$WorkerZipPath,

    [Parameter(Mandatory = $false)]
    [string]$AzureFunctionsHostZipPath
)

Import-Module (Join-Path $PSScriptRoot "scripts/NuGet.Insights.psm1")

$resourceSettings = Get-ResourceSettings $ConfigName $StampName

# Optionally, add the current (deploying) user as an allowed user for the website admin panel.
if ($AllowDeployUser) {
    $context = Get-AzContext
    $homeAccountId = $context.Account.ExtendedProperties.HomeAccountId
    if (!$homeAccountId) {
        throw "Could not find the 'HomeAccountId' from (Get-AzContext).Account.ExtendedProperties.HomeAccountId. Did you run Connect-AzAccount?"
    }
    else {
        if (!$resourceSettings.WebsiteConfig['NuGet.Insights'].AllowedUsers) {
            $resourceSettings.WebsiteConfig['NuGet.Insights'].AllowedUsers = @()
        }

        $objectId, $tenantId = $homeAccountId.Split('.', 2)
        Write-Status "Adding allowed user:"
        Write-Status "  Tenant ID: $tenantId"
        Write-Status "  Object ID: $objectId"

        $resourceSettings.WebsiteConfig['NuGet.Insights'].AllowedUsers += @{
            TenantId = $tenantId;
            ObjectId = $objectId
        }
    }
}


# Publish (build and package) the app code
$deploymentDir = Join-Path $PSScriptRoot "../artifacts/deploy"
function Publish-Project ($ProjectName) {
    Write-Status "Publishing project '$ProjectName'..."
    dotnet publish (Join-Path $PSScriptRoot "../src/$ProjectName") `
        "/p:DeploymentDir=$deploymentDir" `
        --configuration Release `
        --runtime $RuntimeIdentifier `
        --self-contained false | Out-Default
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish $ProjectName."
    }
    
    $path = Join-Path $deploymentDir "$ProjectName.zip"

    return $path.ToString()
}

if (!$WebsiteZipPath) { $WebsiteZipPath = Publish-Project "Website" }
if (!$WorkerZipPath) { $WorkerZipPath = Publish-Project "Worker" }
if (!$AzureFunctionsHostZipPath) {
    if (!(Test-Path $deploymentDir)) { New-Item $deploymentDir -ItemType Directory | Out-Null }
    $AzureFunctionsHostZipPath = Join-Path $deploymentDir "AzureFunctionsHost.zip"
    . (Join-Path $PSScriptRoot "build-host.ps1") `
        -RuntimeIdentifier $RuntimeIdentifier `
        -OutputPath $AzureFunctionsHostZipPath
}

$parameters = @{
    ResourceSettings          = $resourceSettings;
    DeploymentDir             = $deploymentDir;
    WebsiteZipPath            = $WebsiteZipPath;
    WorkerZipPath             = $WorkerZipPath;
    AzureFunctionsHostZipPath = $AzureFunctionsHostZipPath;
}

Write-Status "Using the following deployment parameters:"
ConvertTo-Json $parameters -Depth 100 | Out-Default

Approve-SubscriptionId $resourceSettings.SubscriptionId

. (Join-Path $PSScriptRoot "scripts/Invoke-Deploy.ps1") @parameters
