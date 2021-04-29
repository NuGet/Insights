[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[a-z0-9]+$")]
    [ValidateLength(1, 19)] # 19 because storage accounts and Key Vaults have max 24 characters and the prefix is "expkg".
    [string]$StackName,

    [Parameter(Mandatory = $true)]
    [string]$DeploymentDir,

    [Parameter(Mandatory = $true)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $true)]
    [string]$WorkerZipPath,

    [Parameter(Mandatory = $false)]
    [string]$Location = "West US 2",
    
    [Parameter(Mandatory = $false)]
    [string]$ExistingWebsitePlanId,
    
    [Parameter(Mandatory = $false)]
    [string]$WebsiteName,
    
    [Parameter(Mandatory = $true)]
    [ValidateSet("Y1", "S1", "P1v2")]
    [string]$WorkerSku,
    
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

Write-Status ""
Write-Status "Beginning the deployment process..."

$deploymentId = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")
Write-Status "Using deployment ID: $deploymentId"

# Shared variables and functions
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

# Set up some default config based on worker SKU
if ($WorkerSku -eq "Y1") {
    if ("NuGetPackageExplorerToCsv" -notin $WorkerConfig["Knapcode.ExplorePackages"].DisabledDrivers) {
        # Default "MoveTempToHome" to be true when NuGetPackageExplorerToCsv is enabled. We do this because the NuGet
        # Package Explorer symbol validation APIs are hard-coded to use TEMP and can quickly fill up the small TEMP
        # capacity on consumption plan (~500 MiB). Therefore, we move TEMP to HOME at the start of the process. HOME
        # points to a Azure Storage File share which has no capacity issues.
        if ($null -eq $WorkerConfig["Knapcode.ExplorePackages"].MoveTempToHome) {
            $WorkerConfig["Knapcode.ExplorePackages"].MoveTempToHome = $true
        }

        # Default the maximum number of workers per Function App plan to 16 when NuGetPackageExplorerToCsv is enabled.
        # We do this because it's easy for a lot of Function App workers to overload the HOME directory which is backed
        # by an Azure Storage File share.
        if ($null -eq $WorkerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT) {
            $WorkerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT = 16
        }

        # Default the storage queue trigger batch size to 1 when NuGetPackageExplorerToCsv is enabled. We do this to
        # eliminate the parallelism in the worker process so that we can easily control the number of total parallel
        # queue messages are being processed and therefore are using the HOME file share.
        if ($null -eq $WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize) {
            $WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize = 1
        }
    }
}

function New-Deployment($DeploymentName, $BicepPath, $Parameters) {
    # Docs: https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/parameter-files
    $deploymentParameters = @{
        "`$schema"     = "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#";
        contentVersion = "1.0.0.0";
        parameters     = @{}
    }

    foreach ($key in $Parameters.Keys) {
        $deploymentParameters.parameters.$key = @{ value = $parameters.$key }
    }

    $parametersPath = Join-Path $DeploymentDir "$DeploymentName.deploymentParameters.json"
    $deploymentParameters | ConvertTo-Json -Depth 100 | Out-File $parametersPath -Encoding UTF8

    $fullBicepPath = Join-Path $PSScriptRoot $BicepPath

    return New-AzResourceGroupDeployment `
        -TemplateFile $fullBicepPath `
        -ResourceGroupName $resourceGroupName `
        -Name "$deploymentId-$DeploymentName" `
        -TemplateParameterFile $parametersPath
}

# Make sure the resource group is created
Write-Status "Ensuring the resource group '$resourceGroupName' exists..."
New-AzResourceGroup -Name $resourceGroupName -Location $Location -Force

# Deploy the storage account, Key Vault, and deployment container.
Write-Status "Ensuring the storage account, Key Vault, and deployment container exist..."
New-Deployment `
    -DeploymentName "storage-and-kv" `
    -BicepPath "../storage-and-kv.bicep" `
    -Parameters @{
    storageAccountName      = $storageAccountName;
    keyVaultName            = $keyVaultName;
    identities              = @();
    deploymentContainerName = $deploymentContainerName;
    leaseContainerName      = $leaseContainerName
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

$websiteZipUrl = New-DeploymentZip $WebsiteZipPath "Website-$deploymentId.zip"
$workerZipUrl = New-DeploymentZip $WorkerZipPath "Worker-$deploymentId.zip"

# To workaround limitations in initial deployment Azure Functions, we check the current state and may need to
# deploy a second time.
Write-Status "Counting existing function apps..."
$existingWorkers = Get-AzFunctionApp -ResourceGroupName $resourceGroupName
$existingWorkerCount = $existingWorkers.Count

function New-MainDeployment($deploymentName, $useKeyVaultReference) {
    $parameters = @{
        stackName                     = $StackName;
        storageAccountName            = $storageAccountName;
        keyVaultName                  = $keyVaultName;
        deploymentContainerName       = $deploymentContainerName;
        leaseContainerName            = $leaseContainerName;
        sasConnectionStringSecretName = $sasConnectionStringSecretName;
        sasDefinitionName             = $sasDefinitionName;
        sasValidityPeriod             = $sasValidityPeriod.ToString();
        websiteName                   = $WebsiteName;
        websiteAadClientId            = $aadApp.ApplicationId;
        websiteConfig                 = @($websiteConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        websiteZipUrl                 = $websiteZipUrl;
        workerConfig                  = @($workerConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        workerLogLevel                = $WorkerLogLevel;
        workerSku                     = $workerSku;
        workerZipUrl                  = $workerZipUrl;
        workerCount                   = $workerCount;
        useKeyVaultReference          = $useKeyVaultReference
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
    New-MainDeployment "prepare" $false | Tee-Object -Variable 'deployment'
}
else {
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

if ($existingWorkerCount -lt $workerCount) {
    Write-Status "Deploying again with Key Vault references..."
    New-MainDeployment "main" $true
}

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
        }
        catch {
            if ($attempt -lt 10 -and $_.Exception.Response.StatusCode -ge 500) {
                Start-Sleep -Seconds 5
                continue
            }
            else {
                throw
            }
        }
    }
}
