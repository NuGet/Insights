# Testing NuGet Insights

The NuGet Insights tests use Azure storage heavily. There are three options for fulfilling this storage requirement and
an additional option for testing Kusto integration.

1. [Use in-memory storage stubs](#use-in-memory-storage-stubs)
   - This is the default if no `NUGETINSIGHT_*` environment variables are set.
   - Tests run very quickly but if new storage APIs are used, the stubs need to be enhanced.
2. [Use storage emulator](#use-storage-emulator)
   - Set environment variable `NUGETINSIGHTS_USEDEVELOPMENTSTORAGE` to `true`.
   - Start all Azurite endpoints (blob, queue, tables).
3. [Use real Azure storage](#use-real-azure-storage)
   - Set environment variable `NUGETINSIGHTS_STORAGEACCOUNTNAME` to the name of your storage account and set permissions.
4. [Use real Azure storage and real Kusto](#use-real-azure-storage-and-real-kusto)
   - Same as the previous option but tests ingestion into Azure Data Explorer (Kusto).

## Use in-memory storage stubs

No set up is required. Just run `dotnet test` with no `NUGETINSIGHT_*` environment variables set. Note that there are
some tests that will not be run because they require either a storage emulator, real Azure storage, or even Kusto.

## Use storage emulator

To use this option, all you need running is Azurite. See [Install
Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#install-azurite)
on for documentation on how to get Azurite. You need to start the blob, queue, and table services. Azurite starts all
three by default. A couple tests won't run without real storage or Kusto configuration but they will be gracefully
skipped. If you have VS Code, you can install the Azurite extension a start the Azurite services in the VS Code status
bar.

To avoid disk space management and to improve performance, consider enabling [in-memory
persistence](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#in-memory-persistence).

## Use real Azure storage

The tests can be configured to run against real Azure Storage. To do this, you need to set up test resources, configure
authentication, and configure role assignments (permissions). 

To minimize credential management, it's recommended to give your current user (your own identity) the required blob,
queue, table role assignments on the storage account:

- [Storage Blob Data Contributor](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/storage#storage-blob-data-contributor)
- [Storage Queue Data Contributor](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/storage#storage-queue-data-contributor)
- [Storage Table Data Contributor](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/storage#storage-queue-data-contributor)

[`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet)
is used by default for getting tokens for Azure resources so you can just sign in as yourself with Azure CLI or Azure
PowerShell (both of which are token providers for `DefaultAzureCredential`).

Here's are some Azure PowerShell (Az) commands you can do to create a storage account and Azure Data Explorer resource
with the proper permissions.

```powershell
$resourceGroupName = "joel-insights-tests"
$storageAccountName = "joelinsightstests"
$region = "northcentralus"
$signInName = (Get-AzContext).Account.Id

# create the resource group, completes very quickly
New-AzResourceGroup -Name $resourceGroupName `
  -Location $region

# create the storage account, usually takes less than 1 minute
New-AzStorageAccount -ResourceGroupName $resourceGroupName `
  -Name $storageAccountName `
  -Location $region `
  -SkuName Standard_LRS `
  -Kind StorageV2

$storageAccount = Get-AzStorageAccount -ResourceGroupName $resourceGroupName `
    -Name $storageAccountName

# assign permissions on the storage account
New-AzRoleAssignment -SignInName $signInName `
    -Scope $storageAccount.Id `
    -RoleDefinitionName "Storage Blob Data Contributor"
New-AzRoleAssignment -SignInName $signInName `
    -Scope $storageAccount.Id `
    -RoleDefinitionName "Storage Queue Data Contributor"
New-AzRoleAssignment -SignInName $signInName `
    -Scope $storageAccount.Id `
    -RoleDefinitionName "Storage Table Data Contributor"

# set test environment variables for the created resources
$settings = @{
  "NUGETINSIGHTS_STORAGEACCOUNTNAME" = $storageAccountName;
}

foreach ($pair in $settings.GetEnumerator()) {
  Write-Host "Setting $($pair.Key)..."
  [Environment]::SetEnvironmentVariable($pair.Key, $pair.Value)
  [Environment]::SetEnvironmentVariable($pair.Key, $pair.Value, "User")
}
```

## Use real Azure storage and real Kusto

This is the same as the previous option but also set up the tests for ingesting into Kusto.

```powershell
$resourceGroupName = "joel-insights-tests"
$storageAccountName = "joelinsightstests"
$kustoClusterName = "joelinsightstests"
$kustoDatabaseName = "JoelTestDb"
$region = "northcentralus"
$signInName = (Get-AzContext).Account.Id

# create the resource group, completes very quickly
New-AzResourceGroup -Name $resourceGroupName `
  -Location $region

# create the storage account, usually takes less than 1 minute
New-AzStorageAccount -ResourceGroupName $resourceGroupName `
  -Name $storageAccountName `
  -Location $region `
  -SkuName Standard_LRS `
  -Kind StorageV2

$storageAccount = Get-AzStorageAccount -ResourceGroupName $resourceGroupName `
    -Name $storageAccountName

# assign permissions on the storage account
New-AzRoleAssignment -SignInName $signInName `
    -Scope $storageAccount.Id `
    -RoleDefinitionName "Storage Blob Data Contributor"
New-AzRoleAssignment -SignInName $signInName `
    -Scope $storageAccount.Id `
    -RoleDefinitionName "Storage Queue Data Contributor"
New-AzRoleAssignment -SignInName $signInName `
    -Scope $storageAccount.Id `
    -RoleDefinitionName "Storage Table Data Contributor"

# create the Kusto account, takes several minutes to complete
New-AzKustoCluster -ResourceGroupName $resourceGroupName `
    -Name $kustoClusterName `
    -Location $region `
    -SkuName "Dev(No SLA)_Standard_E2a_v4" `
    -SkuTier "Basic"

New-AzKustoDatabase -ResourceGroupName $resourceGroupName `
    -ClusterName $kustoClusterName `
    -Name $kustoDatabaseName `
    -Kind ReadWrite `
    -Location $region

$kustoCluster = Get-AzKustoCluster -ResourceGroupName $resourceGroupName `
    -Name $kustoClusterName

# set test environment variables for the created resources
$settings = @{
  "NUGETINSIGHTS_STORAGEACCOUNTNAME" = $storageAccountName;
  "NUGETINSIGHTS_KUSTOCONNECTIONSTRING" = "$($kustoCluster.Uri); Fed=true";
  "NUGETINSIGHTS_KUSTODATABASENAME" = $kustoDatabaseName;
}

foreach ($pair in $settings.GetEnumerator()) {
  Write-Host "Setting $($pair.Key)..."
  [Environment]::SetEnvironmentVariable($pair.Key, $pair.Value)
  [Environment]::SetEnvironmentVariable($pair.Key, $pair.Value, "User")
}
```

## Clear the settings

You can use this similar script to clear the environment variables.

```powershell
$settings = Get-ChildItem env: | Where-Object { $_.Name -like "NUGETINSIGHTS_*" }

foreach ($pair in $settings) {
  Write-Host "Clearing $($pair.Name)..."
  [Environment]::SetEnvironmentVariable($pair.Name, $null)
  [Environment]::SetEnvironmentVariable($pair.Name, $null, "User")
}
```

## Environment variable reference

- `NUGETINSIGHTS_USEMEMORYSTORAGE`
  - **Readable name**: use memory storage
  - **Purpose**: use the in-memory implementation of storage (blob, queue, table) service clients. Defaults to `true`
    with a fake storage account name if `NUGETINSIGHTS_USEDEVELOPMENTSTORAGE` is unset and
    `NUGETINSIGHTS_STORAGEACCOUNTNAME` is unset.

- `NUGETINSIGHTS_USEDEVELOPMENTSTORAGE`
  - **Readable name**: use development storage
  - **Purpose**: force the storage emulator endpoints to be used if set to `true`.

- `NUGETINSIGHTS_STORAGEACCOUNTNAME`
  - **Readable name**: storage account name
  - **Purpose**: The storage account name to test against. This is assumed to be in global Azure.
  
- `NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID`
  - **Readable name**: storage client application ID
  - **Purpose**: An AAD app registration application (client) ID which has access to the storage account. It will be a
    GUID.
  
- `NUGETINSIGHTS_STORAGECLIENTTENANTID`
  - **Readable name**: storage client tenant ID
  - **Purpose**: The tenant ID matching `NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID`. It will be a GUID.
  
- `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEPATH`
  - **Readable name**: storage client certificate path
  - **Purpose**: A file path to a certificate usable as a credential for app registration specified by
    `NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID`.
  
- `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULT`
  - **Readable name**: storage client certificate Key Vault
  - **Purpose**: A Key Vault endpoint used for fetching a certificate credential for
    `NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID` in AAD authentication. This is an alternative to
    `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEPATH`. The Azure SDK
    [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet)
    will be used for authorization with the Key Vault. This will likely be your personal profile if you have Az CLI
    installed. It will be an HTTPS URL where the Key Vault name is in the first part of the domain, like
    `https://my-test-kv.vault.azure.net/`.
  
- `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULTCERTIFICATENAME`
  - **Readable name**: storage client certificate Key Vault certificate name
  - **Purpose**: The name of the certificate in `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULT` to use as a credential
    for AAD authentication.

- `NUGETINSIGHTS_KUSTOCONNECTIONSTRING`
  - **Readable name**: Kusto connection string
  - **Purpose**: A connection string used with the Kusto client library for specifying the Kusto (Azure Data Explorer)
    cluster URL, along with other supported connection string parameters. Note that the ingestion endpoint of the
    provided cluster URL will be generated at runtime using the `ingest-` prefix convention.

- `NUGETINSIGHTS_KUSTODATABASENAME`
  - **Readable name**: Kusto database name
  - **Purpose**: The name of the Kusto (Azure Data Explorer) database name within the cluster specified by
    `NUGETINSIGHTS_KUSTOCONNECTIONSTRING` which will be used for ingestion testing.

- `NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEPATH`
  - **Readable name**: Kusto client certificate path
  - **Purpose**: A file path to a certificate usable as a credential for app registration specified by
    `NUGETINSIGHTS_KUSTOCONNECTIONSTRING` (e.g. the `AppClientId=...` connection string property).
  
- `NUGETINSIGHTS_KUSTCLIENTCERTIFICATEKEYVAULT`
  - **Readable name**: Kusto client certificate Key Vault
  - **Purpose**: A Key Vault endpoint used for fetching a certificate credential for
    `NUGETINSIGHTS_KUSTOCONNECTIONSTRING` in AAD authentication. This is an alternative to
    `NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEPATH`. The Azure SDK
    [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet)
    will be used for authorization with the Key Vault. This will likely be your personal profile if you have Az CLI
    installed. It will be an HTTPS URL where the Key Vault name is in the first part of the domain, like
    `https://my-test-kv.vault.azure.net/`.
  
- `NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEKEYVAULTCERTIFICATENAME`
  - **Readable name**: Kusto client certificate Key Vault certificate name
  - **Purpose**: The name of the certificate in `NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEKEYVAULT` to use as a credential
    for AAD authentication.
