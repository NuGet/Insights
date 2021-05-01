using module "./ExplorePackages.psm1"
using namespace ExplorePackages

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [ResourceSettings]$ResourceSettings,
    
    [Parameter(Mandatory = $true)]
    [string]$DeploymentId,

    [Parameter(Mandatory = $true)]
    [string]$DeploymentDir,

    [Parameter(Mandatory = $true)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $true)]
    [string]$WorkerZipPath
)

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

# Prepare the storage and Key Vault
$sasToken = . (Join-Path $PSScriptRoot "Invoke-Prepare.ps1") `
    -ResourceSettings $ResourceSettings `
    -DeploymentId $DeploymentId `
    -DeploymentDir $DeploymentDir

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

$websiteZipUrl = New-DeploymentZip $WebsiteZipPath "Website-$DeploymentId.zip"
$workerZipUrl = New-DeploymentZip $WorkerZipPath "Worker-$DeploymentId.zip"

# Deploy the resources using the main ARM template
Write-Status "Deploying the resources..."
New-Deployment `
    -ResourceGroupName $ResourceSettings.ResourceGroupName `
    -DeploymentDir $DeploymentDir `
    -DeploymentId $DeploymentId `
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
