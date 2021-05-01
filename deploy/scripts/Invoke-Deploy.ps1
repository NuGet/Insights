using module "./ExplorePackages.psm1"
using namespace ExplorePackages

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [ResourceSettings]$ResourceSettings,

    [Parameter(Mandatory = $true)]
    [string]$DeploymentDir,

    [Parameter(Mandatory = $true)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $true)]
    [string]$WorkerZipPath
)

Write-Status ""
Write-Status "Beginning the deployment process..."

$deploymentId = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")
Write-Status "Using deployment ID: $deploymentId"

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
        -ResourceGroupName $ResourceSettings.ResourceGroupName `
        -Name "$deploymentId-$DeploymentName" `
        -TemplateParameterFile $parametersPath
}

# Make sure the resource group is created
Write-Status "Ensuring the resource group '$($ResourceSettings.ResourceGroupName)' exists..."
New-AzResourceGroup -Name $ResourceSettings.ResourceGroupName -Location $ResourceSettings.Location -Force

# Verify the number of function app is not decreasing. This is not supported by the script.
Write-Status "Counting existing function apps..."
$existingWorkers = Get-AzFunctionApp -ResourceGroupName $ResourceSettings.ResourceGroupName
$existingWorkerCount = $existingWorkers.Count
if ($existingWorkerCount -gt $ResourceSettings.WorkerCount) {
    # Would need to:
    # - Delete function apps
    # - Remove managed identity from KV policy (maybe done automatically by ARM)
    # - Delete the File Share (WEBSITE_CONTENTSHARE) created by the function app
    throw 'Reducing the number of workers is not supported.'
}

# Deploy the storage account, Key Vault, and deployment container.
Write-Status "Ensuring the storage account, Key Vault, and deployment container exist..."
New-Deployment `
    -DeploymentName "storage-and-kv" `
    -BicepPath "../storage-and-kv.bicep" `
    -Parameters @{
    storageAccountName      = $ResourceSettings.StorageAccountName;
    keyVaultName            = $ResourceSettings.KeyVaultName;
    identities              = @();
    deploymentContainerName = $ResourceSettings.DeploymentContainerName;
    leaseContainerName      = $ResourceSettings.LeaseContainerName
}

# Manage the storage account in Key Vault
$sasToken = . (Join-Path $PSScriptRoot "Set-KeyVaultManagedStorage.ps1") `
    -ResourceGroupName $ResourceSettings.ResourceGroupName `
    -KeyVaultName $ResourceSettings.KeyVaultName `
    -StorageAccountName $ResourceSettings.StorageAccountName `
    -SasDefinitionName $ResourceSettings.SasDefinitionName `
    -SasConnectionStringSecretName $ResourceSettings.SasConnectionStringSecretName `
    -AutoRegenerateKey:$ResourceSettings.AutoRegenerateKey `
    -SasValidityPeriod $ResourceSettings.SasValidityPeriod

# Initialize the AAD app
$aadApp = (. (Join-Path $PSScriptRoot "Initialize-AadApp.ps1") -AadAppName $ResourceSettings.AadAppName)

# Make the app service support the website for login
function Get-AppServiceBaseUrl($name) {
    "https://$($name.ToLowerInvariant()).azurewebsites.net"
}
. (Join-Path $PSScriptRoot "Initialize-AadAppForWebsite.ps1") `
    -ObjectId $aadApp.ObjectId `
    -BaseUrl (Get-AppServiceBaseUrl $ResourceSettings.WebsiteName)

# Upload the project ZIPs
$storageContext = New-AzStorageContext `
    -StorageAccountName $ResourceSettings.StorageAccountName `
    -SasToken $sasToken

function New-DeploymentZip ($ZipPath, $BlobName) {
    Write-Status "Uploading the ZIP to '$BlobName'..."
    $blob = Set-AzStorageBlobContent `
        -Context $storageContext `
        -Container $Resourcesettings.DeploymentContainerName `
        -File $ZipPath `
        -Blob $BlobName
    return $blob.BlobClient.Uri.AbsoluteUri
}

$websiteZipUrl = New-DeploymentZip $WebsiteZipPath "Website-$deploymentId.zip"
$workerZipUrl = New-DeploymentZip $WorkerZipPath "Worker-$deploymentId.zip"

# Deploy the resources using the main ARM template
function New-MainDeployment($deploymentName) {
    $parameters = @{
        stackName                     = $ResourceSettings.StackName;
        storageAccountName            = $ResourceSettings.StorageAccountName;
        keyVaultName                  = $ResourceSettings.KeyVaultName;
        deploymentContainerName       = $ResourceSettings.DeploymentContainerName;
        leaseContainerName            = $ResourceSettings.LeaseContainerName;
        sasConnectionStringSecretName = $ResourceSettings.SasConnectionStringSecretName;
        sasDefinitionName             = $ResourceSettings.SasDefinitionName;
        sasValidityPeriod             = $ResourceSettings.SasValidityPeriod.ToString();
        websiteName                   = $ResourceSettings.WebsiteName;
        websiteAadClientId            = $aadApp.ApplicationId;
        websiteConfig                 = @($ResourceSettings.WebsiteConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        websiteZipUrl                 = $websiteZipUrl;
        workerNamePrefix              = $ResourceSettings.WorkerNamePrefix;
        workerConfig                  = @($ResourceSettings.WorkerConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        workerLogLevel                = $ResourceSettings.WorkerLogLevel;
        workerSku                     = $ResourceSettings.WorkerSku;
        workerZipUrl                  = $workerZipUrl;
        workerCount                   = $ResourceSettings.WorkerCount
    }

    if ($ResourceSettings.ExistingWebsitePlanId) {
        $parameters.WebsitePlanId = $ResourceSettings.ExistingWebsitePlanId
    }

    New-Deployment `
        -DeploymentName $deploymentName `
        -BicepPath "../main.bicep" `
        -Parameters $parameters
}

Write-Status "Deploying the resources..."
New-MainDeployment "main"

# Warm up the workers, since initial deployment appears to leave them in a hibernation state.
Write-Status "Warming up the website and workers..."
foreach ($appName in @($ResourceSettings.WebsiteName) + (0..($ResourceSettings.WorkerCount - 1) | ForEach-Object { $ResourceSettings.WorkerNamePrefix + $_ })) {
    $url = "$(Get-AppServiceBaseUrl $appName)/"    
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
