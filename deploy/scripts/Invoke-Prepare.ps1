[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    $ResourceSettings,
    
    [Parameter(Mandatory = $false)]
    [string]$DeploymentLabel,

    [Parameter(Mandatory = $false)]
    [string]$DeploymentDir
)

Import-Module (Join-Path $PSScriptRoot "NuGet.Insights.psm1")

$DeploymentLabel, $DeploymentDir = Get-DeploymentLocals $DeploymentLabel $DeploymentDir

# Make sure the resource group is created
Write-Status "Ensuring the resource group '$($ResourceSettings.ResourceGroupName)' exists..."
New-AzResourceGroup `
    -Name $ResourceSettings.ResourceGroupName `
    -Location $ResourceSettings.Location `
    -Force `
    -ErrorAction Stop | Out-Default

# Deploy the storage account, Key Vault, and deployment container.
Write-Status "Ensuring the storage account, Key Vault, and base containers exist..."
New-Deployment `
    -ResourceGroupName $ResourceSettings.ResourceGroupName `
    -DeploymentDir $DeploymentDir `
    -DeploymentLabel $DeploymentLabel `
    -DeploymentName "storage-and-kv" `
    -BicepPath "../bicep/storage-and-kv.bicep" `
    -Parameters @{
    location           = $ResourceSettings.Location;
    storageAccountName = $ResourceSettings.StorageAccountName;
    keyVaultName       = $ResourceSettings.KeyVaultName;
    leaseContainerName = $ResourceSettings.LeaseContainerName
} | Out-Default

# Manage the storage account in Key Vault
. (Join-Path $PSScriptRoot "Set-KeyVaultManagedStorage.ps1") `
    -ResourceGroupName $ResourceSettings.ResourceGroupName `
    -KeyVaultName $ResourceSettings.KeyVaultName `
    -StorageAccountName $ResourceSettings.StorageAccountName `
    -AutoRegenerateKey:$ResourceSettings.AutoRegenerateKey `
    -RegenerationPeriod $ResourceSettings.RegenerationPeriod

# Initialize the AAD app, if necessary
if (!$ResourceSettings.WebsiteAadAppClientId) {
    $aadApp = (. (Join-Path $PSScriptRoot "Initialize-AadApp.ps1") -AadAppName $ResourceSettings.WebsiteAadAppName)

    # Make the app service support the website for login
    . (Join-Path $PSScriptRoot "Initialize-AadAppForWebsite.ps1") `
        -ObjectId $aadApp.id `
        -BaseUrl (Get-AppServiceBaseUrl $ResourceSettings.WebsiteName)

    $ResourceSettings.WebsiteAadAppClientId = $aadApp.appId
    Write-Host "The AAD app registration client ID is $($aadApp.appId)."
}
