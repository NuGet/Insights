[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountName,
    
    [Parameter(Mandatory = $true)]
    [string]$UserPrincipalName,
    
    [Parameter(Mandatory = $true)]
    [string]$SasDefinitionName
)

. (Join-Path $PSScriptRoot "common.ps1")

# This is the key for Azure Storage Emulator, just for creating a template SAS.
# Source: https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator
$keyVaultSpAppId = "cfa8b339-82a2-471a-a3c9-0fc0be7a4093"
$storageEmulatorKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="

Write-Status "Adding 'list', 'set', 'setsas' storage Key Vault permissions for '$UserPrincipalName'..."
Set-AzKeyVaultAccessPolicy `
    -VaultName $KeyVaultName `
    -UserPrincipalName $UserPrincipalName `
    -PermissionsToStorage list,set,setsas

Write-Status "Getting the resource ID for storage account '$StorageAccountName'..."
$storageAccount = Get-AzStorageAccount `
    -ResourceGroupName $ResourceGroupName `
    -Name $StorageAccountName

Write-Status "Checking if Key Vault '$KeyVaultName' already manages storage account '$StorageAccountName'..."
$matchingStorage = Get-AzKeyVaultManagedStorageAccount `
    -VaultName $KeyVaultName `
    | Where-Object { $_.AccountResourceId -eq $storageAccount.Id }
if (!$matchingStorage) {   
    Write-Status "Giving Key Vault the operator role on storage account '$StorageAccountName'..."
    New-AzRoleAssignment `
        -ApplicationId $keyVaultSpAppId `
        -RoleDefinitionName 'Storage Account Key Operator Service Role' `
        -Scope $storageAccount.Id

    Write-Status "Making Key Vault '$KeyVaultName' manage storage account '$StorageAccountName'..."
    $attempt = 0;
    while ($true) {
        try {
            $attempt++
            Add-AzKeyVaultManagedStorageAccount `
                -VaultName $KeyVaultName `
                -AccountName $StorageAccountName `
                -ActiveKeyName key1 `
                -RegenerationPeriod (New-TimeSpan -Days 90) `
                -AccountResourceId $storageAccount.Id `
                -ErrorAction Stop
            break
        }
        catch {
            if ($_.Exception.Body.Error.Code -eq "Forbidden" -and ($attempt -lt 5)) {
                $sleep = 30
                Write-Warning "HTTP 403 Forbidden returned. Trying again in $sleep seconds."
                Start-Sleep -Seconds $sleep
            } else {
                throw
            }
        }
    }
}

Write-Status "Generating a template SAS..."
$storageContext = New-AzStorageContext `
    -StorageAccountName $StorageAccountName `
    -Protocol Https `
    -StorageAccountKey $storageEmulatorKey
$sasTemplate = New-AzStorageAccountSASToken `
    -ExpiryTime (Get-Date "2010-01-01Z" -AsUTC) `
    -Permission "acdlpruw" `
    -ResourceType Service,Container,Object `
    -Service Blob,Queue,Table `
    -Protocol HttpsOnly `
    -Context $storageContext

Write-Status "Creating SAS definition '$SasDefinitionName'..."
Set-AzKeyVaultManagedStorageSasDefinition `
    -VaultName $KeyVaultName `
    -AccountName $StorageAccountName `
    -Name $SasDefinitionName `
    -ValidityPeriod (New-TimeSpan -Days 2) `
    -SasType 'account' `
    -TemplateUri $sasTemplate

Write-Status "Deleting Key Vault policy for '$UserPrincipalName'..."
Remove-AzKeyVaultAccessPolicy `
    -VaultName $KeyVaultName `
    -UserPrincipalName $UserPrincipalName
