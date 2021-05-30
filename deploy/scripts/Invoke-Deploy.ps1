[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    $ResourceSettings,
    
    [Parameter(Mandatory = $false)]
    [string]$DeploymentId,

    [Parameter(Mandatory = $false)]
    [string]$DeploymentDir,

    [Parameter(Mandatory = $true)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $true)]
    [string]$WorkerZipPath
)

Import-Module (Join-Path $PSScriptRoot "NuGet.Insights.psm1")

$DeploymentId, $DeploymentDir = Get-DeploymentLocals $DeploymentId $DeploymentDir

# Prepare the storage and Key Vault
. (Join-Path $PSScriptRoot "Invoke-Prepare.ps1") `
    -ResourceSettings $ResourceSettings `
    -DeploymentId $DeploymentId `
    -DeploymentDir $DeploymentDir

# Verify the number of function app is not decreasing. This is not supported by the script.
Write-Status "Counting existing function apps..."
$existingWorkers = Get-AzFunctionApp -ResourceGroupName $ResourceSettings.ResourceGroupName
$existingWorkerCount = $existingWorkers.Count
$deployingWorkerCount = $ResourceSettings.WorkerPlanCount * $ResourceSettings.WorkerCountPerPlan
if ($existingWorkerCount -gt $deployingWorkerCount) {
    # Would need to:
    # - Delete function apps
    # - Remove managed identity from KV policy (maybe done automatically by ARM)
    # - Delete the File Share (WEBSITE_CONTENTSHARE) created by the function app
    throw 'Reducing the number of workers is not supported.'
}

# Upload the project ZIPs
$storageAccountKey = (Get-AzStorageAccountKey `
    -ResourceGroupName $ResourceSettings.ResourceGroupName `
    -Name $ResourceSettings.StorageAccountName)[0].Value
$storageContext = New-AzStorageContext `
    -StorageAccountName $ResourceSettings.StorageAccountName `
    -StorageAccountKey $storageAccountKey

function New-DeploymentZip ($ZipPath, $BlobName) {
    Write-Status "Uploading the ZIP to '$BlobName'..."
    Set-AzStorageBlobContent `
        -Context $storageContext `
        -Container $Resourcesettings.DeploymentContainerName `
        -File $ZipPath `
        -Blob $BlobName | Out-Default
    return New-AzStorageBlobSASToken `
        -Container $ResourceSettings.DeploymentContainerName `
        -Blob $BlobName `
        -Permission r `
        -Protocol HttpsOnly `
        -Context $storageContext `
        -ExpiryTime (Get-Date).AddHours(6) `
        -FullUri
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
foreach ($appName in @($ResourceSettings.WebsiteName) + (0..($deployingWorkerCount - 1) | ForEach-Object { $ResourceSettings.WorkerNamePrefix + $_ })) {
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
