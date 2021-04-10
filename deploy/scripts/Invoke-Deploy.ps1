[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$StackName,

    [Parameter(Mandatory = $false)]
    [string]$Location = "West US 2",
    
    [Parameter(Mandatory = $false)]
    [string]$ExistingWebsitePlanId,
    
    [Parameter(Mandatory = $false)]
    [int]$WorkerCount = 1,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Warning", "Information")]
    [string]$WorkerLogLevel = "Warning",

    [Parameter(Mandatory = $false)]
    [Hashtable]$WebsiteConfig = @{},

    [Parameter(Mandatory = $false)]
    [Hashtable]$WorkerConfig = @{}
)

. (Join-Path $PSScriptRoot "common.ps1")

# Shared variables and functions
$root = Join-Path $PSScriptRoot "../.."
$resourceGroupName = "ExplorePackages-$StackName"
$storageAccountName = "explore$StackName"
$keyVaultName = "explore$StackName"
$aadAppName = "ExplorePackages-$StackName-Website"
$websitePlanName = "ExplorePackages-$StackName-WebsitePlan"
$storageKeySecretName = "$storageAccountName-FullAccessConnectionString"
$sasDefinitionName = "BlobQueueTableFullAccessSas"
$deploymentContainerName = "deployment"

$deploymentId = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")

function New-Deployment($DeploymentName, $BicepPath, $Parameters) {
    $DeploymentName = "$deploymentId-$DeploymentName"

    # Docs: https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/parameter-files
    $deploymentParameters = @{
        "`$schema" = "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#";
        contentVersion = "1.0.0.0";
        parameters = @{}
    }

    foreach ($key in $Parameters.Keys) {
        $deploymentParameters.parameters.$key = @{ value = $parameters.$key }
    }

    $parametersPath = Join-Path $deployDir "$deploymentName.deploymentParameters.json"
    $deploymentParameters | ConvertTo-Json -Depth 100 | Out-File $parametersPath -Encoding UTF8

    return New-AzResourceGroupDeployment `
        -TemplateFile (Join-Path $PSScriptRoot $BicepPath) `
        -ResourceGroupName $resourceGroupName `
        -Name $deploymentName `
        -TemplateParameterFile $parametersPath
}

# Publish the projects
$deployDir = Join-Path $root "artifacts/deploy"
if (Test-Path $deployDir) {
    Remove-Item $deployDir -Recurse
}
New-Item $deployDir -Type Directory | Out-Null

function Publish-Project ($ProjectName) {
    Write-Status "Publishing project '$ProjectName' with deployment ID '$deploymentId'..."
    dotnet publish (Join-Path $root "src/$ProjectName") --configuration Release | Out-Default
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish $ProjectName."
    }
    
    $oldPath = Join-Path $deployDir "$ProjectName.zip"
    $blobName = "$deploymentId-$ProjectName.zip"
    $newPath = Join-Path $deployDir $blobName
    Move-Item $oldPath $newPath -Force
    $newPath = Resolve-Path $newPath
    Write-Host "Moved the ZIP to $newPath."

    return @($newPath, $blobName)
}

$websiteZipPath, $websiteZipBlobName = Publish-Project "ExplorePackages.Website"
$workerZipPath, $workerZipBlobName = Publish-Project "ExplorePackages.Worker"

# Make sure the resource group is created
Write-Status "Ensuring the resource group '$resourceGroupName' exists..."
New-AzResourceGroup -Name $resourceGroupName -Location $Location -Force

# Fetch the existing access policy identities, if any.
# Workaround for https://github.com/Azure/bicep/issues/784#issuecomment-800591002
Write-Status "Finding access policies on the Key Vault '$keyVaultName'..."
$existingKeyVault = Get-AzKeyVault `
    -ResourceGroupName $resourceGroupName `
    | Where-Object { $_.VaultName -eq $keyVaultName }
if ($existingKeyVault) {
    $existingKeyVault = Get-AzKeyVault `
        -ResourceGroupName $resourceGroupName `
        -VaultName $keyVaultName
    $identities = @($existingKeyVault.AccessPolicies `
        | ForEach-Object { @{ tenantId = $_.TenantId; objectId = $_.ObjectId } })
} else {
    $identities = @()
}

# Ensure all of the identities are service principals
$servicePrincipals = @()
foreach ($identity in $identities) {
    $servicePrincipal = Get-AzADServicePrincipal -ObjectId $identity.objectId
    if (!$servicePrincipal) {
        Write-Warning "Removing access policy for object $($identity.objectId) (tenant $($identity.tenantId))."
    } else {
        $servicePrincipals += $identity
    }
}

# Deploy the storage account, Key Vault, and deployment container.
Write-Status "Ensuring the storage account, Key Vault, and deployment container exist..."
$deployment = New-Deployment `
    -DeploymentName "storage-and-kv" `
    -BicepPath "../storage-and-kv.bicep" `
    -Parameters @{
        storageAccountName = $storageAccountName;
        keyVaultName = $keyVaultName;
        identities = $servicePrincipals;
        deploymentContainerName = $deploymentContainerName
    }

# Get the current user
Write-Status "Determining the current user principal name for Key Vault operations..."
$graphToken = Get-AzAccessToken -Resource "https://graph.microsoft.com/"
$graphHeaders = @{ Authorization = "Bearer $($graphToken.Token)" }
$currentUser = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/me" -Headers $graphHeaders
$upn = $currentUser.userPrincipalName

# Manage the storage account in Key Vault
. (Join-Path $PSScriptRoot "Set-KeyVaultManagedStorage.ps1") `
    -ResourceGroupName $resourceGroupName `
    -KeyVaultName $keyVaultName `
    -StorageAccountName $storageAccountName `
    -UserPrincipalName $upn `
    -SasDefinitionName $sasDefinitionName

# Set the currently active key in Key Vault. This is needed for Azure Functions which cannot use SAS in all places.
$activeStorageKey = . (Join-Path $PSScriptRoot "Set-LatestStorageKey.ps1") `
    -ResourceGroupName $resourceGroupName `
    -KeyVaultName $keyVaultName `
    -StorageAccountName $storageAccountName `
    -StorageKeySecretName $storageKeySecretName `
    -UserPrincipalName $upn

# Deploy the server farm, if not provided
if (!$ExistingWebsitePlanId) {
    Write-Status "Ensuring the website plan '$websitePlanName'..."
    $websitePlan = Get-AzAppServicePlan -ResourceGroupName $resourceGroupName -Name $websitePlanName
    if (!$websitePlan) {
        $websitePlan = New-AzAppServicePlan `
            -Name $websitePlanName `
            -ResourceGroupName $resourceGroupName `
            -Location $Location `
            -Tier Basic `
            -WorkerSize Small
    }
    $websitePlanId = $websitePlan.id
} else {
    $websitePlanId = $ExistingWebsitePlanId
}

# Initialize the AAD app
$aadApp = (. (Join-Path $PSScriptRoot "Initialize-AadApp.ps1") -AadAppName $AadAppName)

# Upload the project ZIPs
$storageContext = New-AzStorageContext `
    -StorageAccountName $storageAccountName `
    -StorageAccountKey $activeStorageKey

function New-DeploymentZip ($ZipPath, $BlobName) {
    Write-Status "Uploading the ZIP to '$BlobName'..."
    Set-AzStorageBlobContent `
        -Context $storageContext `
        -Container $deploymentContainerName `
        -File $ZipPath `
        -Blob $BlobName | Out-Default
    
    Write-Status "Generating SAS for deployment..."
    $sas = New-AzStorageBlobSASToken `
        -Context $storageContext `
        -Container $deploymentContainerName `
        -Blob $BlobName `
        -FullUri `
        -Protocol HttpsOnly `
        -Permission "r" `
        -ExpiryTime (Get-Date).ToUniversalTime().AddHours(6)
    return $sas
}

$websiteZipUrl = New-DeploymentZip $websiteZipPath $websiteZipBlobName
$workerZipUrl = New-DeploymentZip $workerZipPath $workerZipBlobName

# To workaround limitations in initial deployment Azure Functions, we check the current state and may need to
# deploy a second time.
Write-Status "Counting existing function apps..."
$existingWorkers = Get-AzFunctionApp -ResourceGroupName $resourceGroupName
$existingWorkerCount = $existingWorkers.Count

function New-MainDeployment($deploymentName, $useKeyVaultReference) {
    New-Deployment `
        -DeploymentName $deploymentName `
        -BicepPath "../main.bicep" `
        -Parameters @{
            stackName = $StackName;
            storageAccountName = $storageAccountName;
            keyVaultName = $keyVaultName;
            storageKeySecretName = $storageKeySecretName;
            sasDefinitionName = $sasDefinitionName;
            websitePlanId = $websitePlanId;
            websiteAadClientId = $aadApp.ApplicationId;
            websiteConfig = $websiteConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs;
            websiteZipUrl = $websiteZipUrl;
            workerConfig = $workerConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs;
            workerLogLevel = $WorkerLogLevel;
            workerZipUrl = $workerZipUrl;
            workerCount = $workerCount;
            useKeyVaultReference = $useKeyVaultReference
        }
}

if ($existingWorkerCount -gt $workerCount) {
    # Would need to:
    # - Delete function apps
    # - Remove managed identity from KV policy (maybe done automatically by ARM)
    # - Delete the File Share (WEBSITE_CONTENTSHARE) created by the function app
    throw 'Reducing the number of workers is not supported.'
}

if ($existingWorkerCount -lt $workerCount) {
    Write-Status "Deploying without Key Vault references because there are new workers..."
    New-MainDeployment "prepare" $false

    Write-Status "Deploying again with with Key Vault references..."
    New-MainDeployment "main" $false | Tee-Object -Variable 'deployment'
} else {
    Write-Status "Deploying the resources..."
    New-MainDeployment "main" $true | Tee-Object -Variable 'deployment'
}

$websiteDefaultHostName = $deployment.Outputs.websiteDefaultHostName.Value
$websiteHostNames = $deployment.Outputs.websiteHostNames.Value.ToString() | ConvertFrom-Json
$workerDefaultHostNames = $deployment.Outputs.workerDefaultHostNames.Value.ToString() | ConvertFrom-Json

# Make the app service support the website for login
. (Join-Path $PSScriptRoot "Initialize-AadAppForWebsite.ps1") `
    -ObjectId $aadApp.ObjectId `
    -DefaultHostName $websiteDefaultHostName `
    -HostNames $websiteHostNames

# Warm up the workers, since initial deployment appears to leave them in a hibernation state.
Write-Status "Warming up the workers and website..."
foreach ($hostName in $workerDefaultHostNames + $websiteDefaultHostName) {
    $url = "https://$hostName/"
    $response = Invoke-WebRequest -Method HEAD -Uri $url -UseBasicParsing
    Write-Host "$url - $($response.StatusCode) $($response.StatusDescription)"
}