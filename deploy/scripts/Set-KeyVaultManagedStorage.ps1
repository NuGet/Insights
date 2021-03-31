[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountName,
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountId,
    
    [Parameter(Mandatory = $true)]
    [string]$UserPrincipalName,
    
    [Parameter(Mandatory = $true)]
    [string]$SasDefinitionName
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

# This is the key for Azure Storage Emulator, just for creating a template SAS.
# Source: https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator
$storageEmulatorKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="

Write-Status "Giving KeyVault the operator role on storage account '$StorageAccountName'..."
Invoke-Call { az role assignment create `
    --role 'Storage Account Key Operator Service Role' `
    --assignee 'https://vault.azure.net' `
    --scope $StorageAccountId `
    --output tsv `
    --query 'id' }

Write-Status "Adding 'set', 'setsas' storage KeyVault permissions for '$UserPrincipalName'..."
Invoke-Call { az keyvault set-policy `
    --name $KeyVaultName `
    --upn $UserPrincipalName `
    --storage-permissions list set setsas `
    --output tsv `
    --query 'id' }

Write-Status "Checking if KeyVault '$KeyVaultName' already manages storage account '$StorageAccountName'..."
Invoke-Call { az keyvault storage list `
    --vault-name $KeyVaultName `
    --query "[?resourceId=='$StorageAccountId'] | length(@)" } | Tee-Object -Variable 'matchingStorage'
$matchingStorage = [int]$matchingStorage
if ($matchingStorage -eq 0) {
    Write-Status "Making KeyVault '$KeyVaultName' manage storage account '$StorageAccountName'..."
    Invoke-Call { az keyvault storage add `
        --vault-name $KeyVaultName `
        --name $StorageAccountName `
        --active-key-name key1 `
        --auto-regenerate-key `
        --regeneration-period P90D `
        --resource-id $StorageAccountId }
}

Write-Status "Generating a template SAS..."
Invoke-Call { az storage account generate-sas `
    --expiry '2010-01-01' `
    --permissions acdlpruw `
    --resource-types sco `
    --services bqt `
    --https-only `
    --account-name $StorageAccountName `
    --account-key $storageEmulatorKey `
    --output tsv } | Tee-Object -Variable 'sasTemplate'

Write-Status "Creating SAS definition '$SasDefinitionName'..."
Invoke-Call { az keyvault storage sas-definition create `
    --vault-name $KeyVaultName `
    --account-name $StorageAccountName `
    --name $SasDefinitionName `
    --validity-period P2D `
    --sas-type account `
    --template-uri "`"$sasTemplate`"" }

Write-Status "Deleting KeyVault policy for '$UserPrincipalName'..."
Invoke-Call { az keyvault delete-policy `
    --name $KeyVaultName `
    --upn $UserPrincipalName `
    --output tsv `
    --query 'id' }
