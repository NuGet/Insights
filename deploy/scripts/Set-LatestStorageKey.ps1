[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountName,

    [Parameter(Mandatory = $true)]
    [string]$StorageKeySecretName,
    
    [Parameter(Mandatory = $true)]
    [string]$UserPrincipalName
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

Write-Status "Adding 'list', 'get', 'set' secret and 'get' storage KeyVault permissions for '$UserPrincipalName'..."
Invoke-Call { az keyvault set-policy `
    --name $KeyVaultName `
    --upn $UserPrincipalName `
    --secret-permissions list get set `
    --storage-permissions get `
    --output tsv `
    --query 'id' }

Write-Status "Determining active key name for storage account '$StorageAccountName' and KeyVault '$KeyVaultName'..."
Invoke-Call { az keyvault storage show `
    --vault-name $KeyVaultName `
    --name $StorageAccountName `
    --query 'activeKeyName' `
    --output tsv } | Tee-Object -Variable 'activeKeyName'

Write-Status "Getting keys for storage account '$StorageAccountName'..."
$keys = Invoke-Call { az storage account keys list `
    --resource-group $ResourceGroupName `
    --account-name $StorageAccountName } | ConvertFrom-Json
Write-Host 'Done.'

$activeKey = $keys | ? { $_.keyName -eq $activeKeyName }

$newSecretValue = "DefaultEndpointsProtocol=https;" +
    "AccountName=$StorageAccountName;" +
    "AccountKey=$($activeKey.value);" +
    "EndpointSuffix=core.windows.net"

Write-Status "Checking if the secret '$StorageKeySecretName' exists..."
Invoke-Call { az keyvault secret list `
    --vault-name $KeyVaultName `
    --query "[?name=='$StorageKeySecretName'] | length(@)" } | Tee-Object -Variable 'matchedSecrets'
$matchedSecrets = [int]$matchedSecrets

if ($matchedSecrets -gt 0) {
    Write-Status "Getting current secret value for secret '$StorageKeySecretName'..."
    $existingSecretValue = Invoke-Call { az keyvault secret show `
        --vault-name $KeyVaultName `
        --name $StorageKeySecretName `
        --query 'value' `
        --output tsv }
    $needsSet = $newSecretValue -ne $existingSecretValue
    if ($needsSet) {
        Write-Host "The secret value has changed."
    }
} else {
    $needsSet = $true
}

if ($needsSet) {
    Write-Status "Setting secret '$StorageKeySecretName'..."
    Invoke-Call { az keyvault secret set `
        --vault-name $KeyVaultName `
        --name $StorageKeySecretName `
        --value $newSecretValue } | Out-Null
    Write-Host "Done."
} else {
    Write-Host "The secret is already up to date."
}

Write-Status "Deleting KeyVault policy for '$UserPrincipalName'..."
Invoke-Call { az keyvault delete-policy `
    --name $KeyVaultName `
    --upn $UserPrincipalName `
    --output tsv `
    --query 'id' }
