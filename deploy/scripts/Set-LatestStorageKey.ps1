[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountName,

    [Parameter(Mandatory = $true)]
    [string]$StorageKeySecretName,
    
    [Parameter(Mandatory = $true)]
    [string]$UserPrincipalName
)

. (Join-Path $PSScriptRoot "common.ps1")

Write-Status "Adding 'list', 'get', 'set' secret and 'get' storage Key Vault permissions for '$UserPrincipalName'..."
Set-AzKeyVaultAccessPolicy `
    -VaultName $KeyVaultName `
    -UserPrincipalName $UserPrincipalName `
    -PermissionsToSecrets list,get,set `
    -PermissionsToStorage get | Out-Default

Write-Status "Determining active key name for storage account '$StorageAccountName' and Key Vault '$KeyVaultName'..."
$managedStorageAccount = Get-AzKeyVaultManagedStorageAccount `
    -VaultName $KeyVaultName `
    -AccountName $StorageAccountName

Write-Status "Getting keys for storage account '$StorageAccountName'..."
$keys = Get-AzStorageAccountKey `
    -ResourceGroupName $resourceGroupName `
    -Name $storageAccountName
Write-Host 'Done.'

$activeKey = $keys | Where-Object { $_.KeyName -eq $managedStorageAccount.ActiveKeyName }

$newSecretValue = "DefaultEndpointsProtocol=https;" +
    "AccountName=$StorageAccountName;" +
    "AccountKey=$($activeKey.Value);" +
    "EndpointSuffix=core.windows.net"

Write-Status "Checking if the secret '$StorageKeySecretName' exists..."
$matchedSecret = Get-AzKeyVaultSecret `
    -VaultName $KeyVaultName `
    | Where-Object { $_.Name -eq $StorageKeySecretName }
if ($matchedSecret) {
    $matchedSecret = Get-AzKeyVaultSecret `
        -VaultName $KeyVaultName `
        -Name $StorageKeySecretName `
        -AsPlainText
    $needsSet = $matchedSecret -ne $newSecretValue
    if ($needsSet) {
        Write-Host "The secret value has changed."
    }
} else {
    $needsSet = $true
}

if ($needsSet) {
    Write-Status "Setting secret '$StorageKeySecretName'..."
    Set-AzKeyVaultSecret `
        -VaultName $KeyVaultName `
        -Name $StorageKeySecretName `
        -SecretValue (ConvertTo-SecureString -String $newSecretValue -AsPlainText) | Out-Default
} else {
    Write-Host "The secret is already up to date."
}

Write-Status "Deleting Key Vault policy for '$UserPrincipalName'..."
Remove-AzKeyVaultAccessPolicy `
    -VaultName $KeyVaultName `
    -UserPrincipalName $UserPrincipalName | Out-Default

$activeKey.Value
