# Testing NuGet Insights

To run the tests, all you need running is Azurite. You need to start the blob,
queue, and table services. Azurite starts all three by default. A couple tests
won't run without Kusto configuration but they will be gracefully skipped.

To avoid disk space management and to improve performance, consider enabling
[in-memory
persistence](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#in-memory-persistence). 

## Real Azure tests

The tests can be configured to run against real Azure Storage and even real
Kusto (Azure Data Explorer). To do this, you need to set up test resources,
configure authentication, and configure role assignments (permissions).

To minimize credential management, it's recommended to give your current user
(your own identity) the required blob, queue, table, and (optionally) Kusto
permissions.

Here's are some Azure PowerShell (Az) commands you can do to create a storage
account and Azure Data Explorer resource with the proper permissions.

```powershell
$resourceGroupName = "jver-insights-tests"
$storageAccountName = "jverinsightstests"
$kustoClusterName = "jverinsightstests"
$kustoDatabaseName = "JverTestDb"
$region = "eastus"
$signInName = (Get-AzContext).Account.Id

# create the storage account, usually takes less than 1 minutes
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

- `NUGETINSIGHTS_STORAGEACCOUNTNAME`
  - **Readable name**: storage account name
  - **Purpose**: The storage account name to test against. This is assumed to be
    in global Azure.
  
- `NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID`
  - **Readable name**: storage client application ID
  - **Purpose**: An AAD app registration application (client) ID which has
    access to the storage account. It will be a GUID.
  
- `NUGETINSIGHTS_STORAGECLIENTTENANTID`
  - **Readable name**: storage client tenant ID
  - **Purpose**: The tenant ID matching
    `NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID`. It will be a GUID.
  
- `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEPATH`
  - **Readable name**: storage client certificate path
  - **Purpose**: A file path to a certificate usable as a credential for app
    registration specified by `NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID`.
  
- `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULT`
  - **Readable name**: storage client certificate Key Vault
  - **Purpose**: A Key Vault endpoint used for fetching a certificate credential
    for `NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID` in AAD authentication. This
    is an alternative to `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEPATH`. The Azure
    SDK
    [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet)
    will be used for authorization with the Key Vault. This will likely be your
    personal profile if you have Az CLI installed. It will be an HTTPS URL where
    the Key Vault name is in the first part of the domain, like
    `https://my-test-kv.vault.azure.net/`.
  
- `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULTCERTIFICATENAME`
  - **Readable name**: storage client certificate Key Vault certificate name
  - **Purpose**: The name of the certificate in
    `NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULT` to use as a credential for
    AAD authentication.

- `NUGETINSIGHTS_STORAGESAS`
  - **Readable name**: storage SAS
  - **Purpose**: A SAS token which has all blob, queue, and table permissions.
    An example SAS token is
    `?sv=2018-03-28&ss=bqt&srt=sco&spr=https&st=2024-03-25T14%3A05%3A53Z&se=2024-04-01T14%3A05%3A53Z&sp=rwdlacup&sig=<signature>`.
  
- `NUGETINSIGHTS_STORAGEBLOBREADSAS`
  - **Readable name**: storage blob read SAS
  - **Purpose**: A SAS token which has blob read permissions. An example SAS
    token is
    `?sv=2018-03-28&ss=b&srt=o&spr=https&st=2024-03-25T14%3A05%3A53Z&se=2024-04-01T14%3A05%3A53Z&sp=rl&sig=<signature>`.

- `NUGETINSIGHTS_KUSTOCONNECTIONSTRING`
  - **Readable name**: Kusto connection string
  - **Purpose**: A connection string used with the Kusto client library for
    specifying the Kusto (Azure Data Explorer) cluster URL, along with other
    supported connection string parameters. Note that the ingestion endpoint of
    the provided cluster URL will be generated at runtime using the `ingest-`
    prefix convention.

- `NUGETINSIGHTS_KUSTODATABASENAME`
  - **Readable name**: Kusto database name
  - **Purpose**: The name of the Kusto (Azure Data Explorer) database name
    within the cluster specified by `NUGETINSIGHTS_KUSTOCONNECTIONSTRING` which
    will be used for ingestion testing.

- `NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEPATH`
  - **Readable name**: Kusto client certificate path
  - **Purpose**: A file path to a certificate usable as a credential for app
    registration specified by `NUGETINSIGHTS_KUSTOCONNECTIONSTRING` (e.g. the
    `AppClientId=...` connection string property).
  
- `NUGETINSIGHTS_KUSTCLIENTCERTIFICATEKEYVAULT`
  - **Readable name**: Kusto client certificate Key Vault
  - **Purpose**: A Key Vault endpoint used for fetching a certificate credential
    for `NUGETINSIGHTS_KUSTOCONNECTIONSTRING` in AAD authentication. This is an
    alternative to `NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEPATH`. The Azure SDK
    [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet)
    will be used for authorization with the Key Vault. This will likely be your
    personal profile if you have Az CLI installed. It will be an HTTPS URL where
    the Key Vault name is in the first part of the domain, like
    `https://my-test-kv.vault.azure.net/`.
  
- `NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEKEYVAULTCERTIFICATENAME`
  - **Readable name**: Kusto client certificate Key Vault certificate name
  - **Purpose**: The name of the certificate in
    `NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEKEYVAULT` to use as a credential for
    AAD authentication.
