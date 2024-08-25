[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountName,
    
    [Parameter(Mandatory = $true)]
    [switch]$AutoRegenerateKey,

    [Parameter(Mandatory = $false)]
    [TimeSpan]$RegenerationPeriod
)

Import-Module (Join-Path $PSScriptRoot "NuGet.Insights.psm1")

# The application ID for Key Vault managed storage:
# Source: https://docs.microsoft.com/en-us/azure/key-vault/secrets/overview-storage-keys-powershell
$keyVaultSpAppId = "cfa8b339-82a2-471a-a3c9-0fc0be7a4093"

$currentUser = Get-AzCurrentUser

Add-AzRoleAssignmentWithRetry $currentUser $ResourceGroupName "Key Vault Administrator" {
    Get-AzKeyVaultManagedStorageAccount `
        -VaultName $KeyVaultName `
        -ErrorAction Stop | Out-Null
}

Write-Status "Getting the resource ID for storage account '$StorageAccountName'..."
$storageAccount = Get-AzStorageAccount `
    -ResourceGroupName $ResourceGroupName `
    -Name $StorageAccountName

Write-Status "Checking if Key Vault '$KeyVaultName' already manages storage account '$StorageAccountName'..."
$matchingStorage = Get-AzKeyVaultManagedStorageAccount `
    -VaultName $KeyVaultName `
    -ErrorAction Stop `
| Where-Object { $_.AccountResourceId -eq $storageAccount.Id }

if (!$matchingStorage) {   
    Write-Status "Giving Key Vault the operator role on storage account '$StorageAccountName'..."
    $roleAssignement = Get-AzRoleAssignment `
        -RoleDefinitionName 'Storage Account Key Operator Service Role' `
        -Scope $storageAccount.Id
    if (!$roleAssignement) {
        New-AzRoleAssignment `
            -ApplicationId $keyVaultSpAppId `
            -RoleDefinitionName 'Storage Account Key Operator Service Role' `
            -Scope $storageAccount.Id | Out-Default
    }

    Write-Status "Making Key Vault '$KeyVaultName' manage storage account '$StorageAccountName'..."
    Add-AzKeyVaultManagedStorageAccount `
        -VaultName $KeyVaultName `
        -AccountName $StorageAccountName `
        -ActiveKeyName key1 `
        -AccountResourceId $storageAccount.Id `
        -ErrorAction Stop | Out-Default
}

Remove-AzRoleAssignmentWithRetry $currentUser $ResourceGroupName "Key Vault Administrator"
