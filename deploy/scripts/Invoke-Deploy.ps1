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
    
    $parametersPath = Join-Path $DeploymentDir "$DeploymentName.deploymentParameters.json"
    New-ParameterFile $Parameters @() $parametersPath

    return New-AzResourceGroupDeployment `
        -TemplateFile (Join-Path $PSScriptRoot $BicepPath) `
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

function Get-AppServiceBaseUrl($name) {
    "https://$($name.ToLowerInvariant()).azurewebsites.net"
}

# Initialize the AAD app, if necessary
if (!$ResourceSettings.WebsiteAadAppClientId) {
    $aadApp = (. (Join-Path $PSScriptRoot "Initialize-AadApp.ps1") -AadAppName $ResourceSettings.WebsiteAadAppName)

    # Make the app service support the website for login
    . (Join-Path $PSScriptRoot "Initialize-AadAppForWebsite.ps1") `
        -ObjectId $aadApp.ObjectId `
        -BaseUrl (Get-AppServiceBaseUrl $ResourceSettings.WebsiteName)

    $ResourceSettings.WebsiteAadAppClientId = $aadApp.ApplicationId
}

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
Write-Status "Deploying the resources..."
New-Deployment `
    -DeploymentName "main" `
    -BicepPath "../main.bicep" `
    -Parameters (New-MainParameters $ResourceSettings $websiteZipUrl $workerZipUrl)

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
                -UseBasicParsing `
                -ErrorAction Stop
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
