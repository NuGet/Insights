[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    $ResourceSettings,
    
    [Parameter(Mandatory = $false)]
    [string]$DeploymentId,

    [Parameter(Mandatory = $false)]
    [string]$DeploymentDir
)

Import-Module (Join-Path $PSScriptRoot "ExplorePackages.psm1")

$DeploymentId, $DeploymentDir = Get-DeploymentLocals $DeploymentId $DeploymentDir

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
    Write-Host "The AAD app registration client ID is $($aadApp.ApplicationId)."
}

$sasToken
