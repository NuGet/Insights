using module "./ExplorePackages.psm1"
using namespace ExplorePackages

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [ResourceSettings]$ResourceSettings,
    
    [Parameter(Mandatory = $true)]
    [string]$DeploymentId,

    [Parameter(Mandatory = $true)]
    [string]$DeploymentDir
)

# Make sure the resource group is created
Write-Status "Ensuring the resource group '$($ResourceSettings.ResourceGroupName)' exists..."
New-AzResourceGroup -Name $ResourceSettings.ResourceGroupName -Location $ResourceSettings.Location -Force | Out-Default

# Deploy the storage account, Key Vault, and deployment container.
Write-Status "Ensuring the storage account, Key Vault, and deployment container exist..."
New-Deployment `
    -ResourceGroupName $ResourceSettings.ResourceGroupName `
    -DeploymentDir $DeploymentDir `
    -DeploymentId $DeploymentId `
    -DeploymentName "storage-and-kv" `
    -BicepPath "../storage-and-kv.bicep" `
    -Parameters @{
    storageAccountName      = $ResourceSettings.StorageAccountName;
    keyVaultName            = $ResourceSettings.KeyVaultName;
    identities              = @();
    deploymentContainerName = $ResourceSettings.DeploymentContainerName;
    leaseContainerName      = $ResourceSettings.LeaseContainerName
} | Out-Default

# Manage the storage account in Key Vault
$sasToken = . (Join-Path $PSScriptRoot "Set-KeyVaultManagedStorage.ps1") `
    -ResourceGroupName $ResourceSettings.ResourceGroupName `
    -KeyVaultName $ResourceSettings.KeyVaultName `
    -StorageAccountName $ResourceSettings.StorageAccountName `
    -SasDefinitionName $ResourceSettings.SasDefinitionName `
    -SasConnectionStringSecretName $ResourceSettings.SasConnectionStringSecretName `
    -AutoRegenerateKey:$ResourceSettings.AutoRegenerateKey `
    -SasValidityPeriod $ResourceSettings.SasValidityPeriod

# Initialize the AAD app, if necessary
if (!$ResourceSettings.WebsiteAadAppClientId) {
    $aadApp = (. (Join-Path $PSScriptRoot "Initialize-AadApp.ps1") -AadAppName $ResourceSettings.WebsiteAadAppName)

    # Make the app service support the website for login
    . (Join-Path $PSScriptRoot "Initialize-AadAppForWebsite.ps1") `
        -ObjectId $aadApp.ObjectId `
        -BaseUrl (Get-AppServiceBaseUrl $ResourceSettings.WebsiteName)

    $ResourceSettings.WebsiteAadAppClientId = $aadApp.ApplicationId
}

$sasToken
