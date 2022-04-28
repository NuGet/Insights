[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    $ResourceSettings,
    
    [Parameter(Mandatory = $false)]
    [string]$DeploymentLabel,

    [Parameter(Mandatory = $false)]
    [string]$DeploymentDir,

    [Parameter(Mandatory = $true)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $true)]
    [string]$WorkerZipPath,

    [Parameter(Mandatory = $true)]
    [string]$AzureFunctionsHostZipPath
)

Import-Module (Join-Path $PSScriptRoot "NuGet.Insights.psm1")

$DeploymentLabel, $DeploymentDir = Get-DeploymentLocals $DeploymentLabel $DeploymentDir

# Prepare the storage and Key Vault
. (Join-Path $PSScriptRoot "Invoke-Prepare.ps1") `
    -ResourceSettings $ResourceSettings `
    -DeploymentLabel $DeploymentLabel `
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

function New-DeploymentFile ($Path, $BlobName) {
    Write-Status "Uploading to '$BlobName'..."
    Set-AzStorageBlobContent `
        -Context $storageContext `
        -Container $Resourcesettings.DeploymentContainerName `
        -File $Path `
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

$workerStandaloneEnvPath = Join-Path $DeploymentDir "WorkerStandalone.env"
New-WorkerStandaloneEnv $ResourceSettings | Out-EnvFile -FilePath $workerStandaloneEnvPath
$installWorkerStandalonePath = Join-Path $PSScriptRoot "Install-WorkerStandalone.ps1"

$websiteZipUrl = New-DeploymentFile $WebsiteZipPath "$DeploymentLabel/Website.zip"
$workerZipUrl = New-DeploymentFile $WorkerZipPath "$DeploymentLabel/Worker.zip"
$azureFunctionsHostZipUrl = New-DeploymentFile $AzureFunctionsHostZipPath "$DeploymentLabel/AzureFunctionsHost.zip"
$workerStandaloneEnvUrl = New-DeploymentFile $workerStandaloneEnvPath "$DeploymentLabel/WorkerStandalone.env"
$installWorkerStandaloneUrl = New-DeploymentFile $installWorkerStandalonePath "$DeploymentLabel/Install-WorkerStandalone.ps1"

# Deploy the resources using the main ARM template
Write-Status "Deploying the resources..."
New-Deployment `
    -ResourceGroupName $ResourceSettings.ResourceGroupName `
    -DeploymentDir $DeploymentDir `
    -DeploymentLabel $DeploymentLabel `
    -DeploymentName "main" `
    -BicepPath "../main.bicep" `
    -Parameters (New-MainParameters $ResourceSettings $websiteZipUrl $workerZipUrl $DeploymentLabel)

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
