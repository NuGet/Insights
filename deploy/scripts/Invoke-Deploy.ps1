[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[a-z0-9]+$")]
    [ValidateLength(1, 19)] # 19 because storage accounts and Key Vaults have max 24 characters and the prefix is "expkg".
    [string]$StackName,

    [Parameter(Mandatory = $false)]
    [string]$Location = "West US 2",
    
    [Parameter(Mandatory = $false)]
    [string]$ExistingWebsitePlanId,
    
    [Parameter(Mandatory = $false)]
    [string]$WebsiteName,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Y1", "S1", "P1v2")]
    [string]$WorkerSku = "Y1", # Y1 is consumption plan
    
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
$storageAccountName = "expkg$($StackName.ToLowerInvariant())"
$keyVaultName = "expkg$($StackName.ToLowerInvariant())"
$aadAppName = "ExplorePackages-$StackName-Website"
$sasConnectionStringSecretName = "$storageAccountName-SasConnectionString"
$sasDefinitionName = "BlobQueueTableFullAccessSas"
$deploymentContainerName = "deployment"
$leaseContainerName = "leases"
$sasValidityPeriod = New-TimeSpan -Days 6

if (!$WebsiteName) {
    $WebsiteName = "ExplorePackages-$StackName"
}

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

    $fullBicepPath = Join-Path $PSScriptRoot $BicepPath

    return New-AzResourceGroupDeployment `
        -TemplateFile $fullBicepPath `
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
New-Deployment `
    -DeploymentName "storage-and-kv" `
    -BicepPath "../storage-and-kv.bicep" `
    -Parameters @{
        storageAccountName = $storageAccountName;
        keyVaultName = $keyVaultName;
        identities = $servicePrincipals;
        deploymentContainerName = $deploymentContainerName;
        leaseContainerName = $leaseContainerName
    }

# Get the current user
Write-Status "Determining the current user principal name for Key Vault operations..."
$graphToken = Get-AzAccessToken -Resource "https://graph.microsoft.com/"
$graphHeaders = @{ Authorization = "Bearer $($graphToken.Token)" }
$currentUser = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/me" -Headers $graphHeaders
$upn = $currentUser.userPrincipalName

# Manage the storage account in Key Vault

# Since Consumption plan requires WEBSITE_CONTENTAZUREFILECONNECTIONSTRING and this does not support SAS-based
# connection strings, don't auto-regenerate in this case. We would need to regularly update a connection string based
# on the active storage access key, which isn't worth the effort for this approach that is less secure anyway.
$autoRegenerateKey = $WorkerSku -ne 'Y1'

$sasToken = . (Join-Path $PSScriptRoot "Set-KeyVaultManagedStorage.ps1") `
    -ResourceGroupName $resourceGroupName `
    -KeyVaultName $keyVaultName `
    -StorageAccountName $storageAccountName `
    -UserPrincipalName $upn `
    -SasDefinitionName $sasDefinitionName `
    -SasConnectionStringSecretName $sasConnectionStringSecretName `
    -AutoRegenerateKey:$autoRegenerateKey `
    -SasValidityPeriod $sasValidityPeriod

# Initialize the AAD app
$aadApp = (. (Join-Path $PSScriptRoot "Initialize-AadApp.ps1") -AadAppName $AadAppName)

# Upload the project ZIPs
$storageContext = New-AzStorageContext `
    -StorageAccountName $storageAccountName `
    -SasToken $sasToken

function New-DeploymentZip ($ZipPath, $BlobName) {
    Write-Status "Uploading the ZIP to '$BlobName'..."
    $blob = Set-AzStorageBlobContent `
        -Context $storageContext `
        -Container $deploymentContainerName `
        -File $ZipPath `
        -Blob $BlobName
    return $blob.BlobClient.Uri.AbsoluteUri
}

$websiteZipUrl = New-DeploymentZip $websiteZipPath $websiteZipBlobName
$workerZipUrl = New-DeploymentZip $workerZipPath $workerZipBlobName

# To workaround limitations in initial deployment Azure Functions, we check the current state and may need to
# deploy a second time.
Write-Status "Counting existing function apps..."
$existingWorkers = Get-AzFunctionApp -ResourceGroupName $resourceGroupName
$existingWorkerCount = $existingWorkers.Count

function New-MainDeployment($deploymentName, $useKeyVaultReference) {
    $parameters = @{
        stackName = $StackName;
        storageAccountName = $storageAccountName;
        keyVaultName = $keyVaultName;
        deploymentContainerName = $deploymentContainerName;
        leaseContainerName = $leaseContainerName;
        sasConnectionStringSecretName = $sasConnectionStringSecretName;
        sasDefinitionName = $sasDefinitionName;
        sasValidityPeriod = $sasValidityPeriod.ToString();
        websiteName = $WebsiteName;
        websiteAadClientId = $aadApp.ApplicationId;
        websiteConfig = @($websiteConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        websiteZipUrl = $websiteZipUrl;
        workerConfig = @($workerConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        workerLogLevel = $WorkerLogLevel;
        workerSku = $workerSku;
        workerZipUrl = $workerZipUrl;
        workerCount = $workerCount;
        useKeyVaultReference = $useKeyVaultReference
    }

    if ($ExistingWebsitePlanId) {
        $parameters.WebsitePlanId = $ExistingWebsitePlanId
    }

    New-Deployment `
        -DeploymentName $deploymentName `
        -BicepPath "../main.bicep" `
        -Parameters $parameters
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

    Write-Status "Deploying again with Key Vault references..."
    New-MainDeployment "main" $true | Tee-Object -Variable 'deployment'
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
Write-Status "Warming up the website and workers..."
foreach ($hostName in @($websiteDefaultHostName) + $workerDefaultHostNames) {
    $url = "https://$hostName/"
    
    $attempt = 0;
    while ($true) {
        $attempt++
        try {
            $response = Invoke-WebRequest `
                -Method HEAD `
                -Uri $url `
                -UseBasicParsing
            Write-Host "$url - $($response.StatusCode) $($response.StatusDescription)"
            break
        } catch {
            if ($attempt -lt 10 -and $_.Exception.Response.StatusCode -ge 500) {
                Start-Sleep -Seconds 5
                continue
            } else {
                throw
            }
        }
    }
}
