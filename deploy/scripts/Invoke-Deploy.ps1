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

    [Parameter(Mandatory = $false)]
    [string]$AzureFunctionsHostZipPath,

    [Parameter(Mandatory = $true)]
    [string]$RuntimeIdentifier,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipPrepare
)

Import-Module (Join-Path $PSScriptRoot "NuGet.Insights.psm1")

$DeploymentLabel, $DeploymentDir = Get-DeploymentLocals $DeploymentLabel $DeploymentDir

if (!$SkipPrepare) {
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
}

# Upload the project ZIPs
$storageContext = New-AzStorageContext `
    -StorageAccountName $ResourceSettings.StorageAccountName `
    -UseConnectedAccount

# Give the current user access
$currentUser = Get-AzCurrentUser

# Disable the storage firewall if it is enabled, it will be restored in the deployment
Write-Status "Disabling the storage account firewall for local deployment..."
Set-StorageFirewallDefaultAction $ResourceSettings "Allow"

Add-AzRoleAssignmentWithRetry $currentUser $ResourceSettings.ResourceGroupName "Storage Blob Data Contributor" {
    $container = Get-AzStorageContainer `
        -Context $storageContext `
        -ErrorAction Stop `
    | Where-Object { $_.Name -eq $Resourcesettings.DeploymentContainerName }
    if (!$container) {
        New-AzStorageContainer `
            -Context $storageContext `
            -Name $Resourcesettings.DeploymentContainerName `
            -ErrorAction Stop | Out-Null 
    }
}

function New-DeploymentFile ($Path, $BlobName) {
    Write-Status "Uploading to '$BlobName'..."

    return Invoke-WithRetryOnForbidden {
        Set-AzStorageBlobContent `
            -Context $storageContext `
            -Container $ResourceSettings.DeploymentContainerName `
            -File $Path `
            -Blob $BlobName `
            -Force `
            -ErrorAction Stop | Out-Null
    
        $sasUrl = New-AzStorageBlobSASToken `
            -Container $ResourceSettings.DeploymentContainerName `
            -Blob $BlobName `
            -Permission r `
            -Protocol HttpsOnly `
            -Context $storageContext `
            -ExpiryTime (Get-Date).AddHours(6) `
            -FullUri `
            -ErrorAction Stop

        # make sure the SAS URL works (handle RBAC propagation delay)
        Invoke-WebRequest -Method HEAD $sasUrl -ErrorAction Stop | Out-Null

        return $sasUrl
    }
}

$workerStandaloneEnvPath = Join-Path $DeploymentDir "WorkerStandalone.env"
New-WorkerStandaloneEnv $ResourceSettings | Out-EnvFile -FilePath $workerStandaloneEnvPath
$installWorkerStandalonePath = Join-Path $PSScriptRoot "Install-WorkerStandalone.ps1"

$dotnetInstallScriptPath = Join-Path $DeploymentDir "dotnet-install.ps1"
Invoke-DownloadDotnetInstallScript $dotnetInstallScriptPath

$websiteZipUrl = New-DeploymentFile $WebsiteZipPath "$DeploymentLabel/Website.zip"
$workerZipUrl = New-DeploymentFile $WorkerZipPath "$DeploymentLabel/Worker.zip"
$spotWorkerCustomScriptExtensionFiles = @()

if ($ResourceSettings.UseSpotWorkers) {
    if (!$AzureFunctionsHostZipPath) {
        throw "No AzureFunctionsHostZipPath parameter was provided but at least one of the configurations has UseSpotWorkers set to true."
    }

    $dotnetInstallScriptUrl = New-DeploymentFile $dotnetInstallScriptPath "$DeploymentLabel/dotnet-install.ps1"
    $azureFunctionsHostZipUrl = New-DeploymentFile $AzureFunctionsHostZipPath "$DeploymentLabel/AzureFunctionsHost.zip"
    $workerStandaloneEnvUrl = New-DeploymentFile $workerStandaloneEnvPath "$DeploymentLabel/WorkerStandalone.env"
    $installWorkerStandaloneUrl = New-DeploymentFile $installWorkerStandalonePath "$DeploymentLabel/Install-WorkerStandalone.ps1"

    $spotWorkerCustomScriptExtensionFiles = @(
        $workerZipUrl,
        $azureFunctionsHostZipUrl,
        $dotnetInstallScriptUrl,
        $workerStandaloneEnvUrl,
        $installWorkerStandaloneUrl
    )
}

# Deploy the resources using the main ARM template
Write-Status "Deploying the main resources..."

$mainParameters = New-MainParameters `
    -ResourceSettings $ResourceSettings `
    -DeploymentLabel $DeploymentLabel `
    -WebsiteZipUrl $websiteZipUrl `
    -WorkerZipUrl $workerZipUrl `
    -SpotWorkerCustomScriptExtensionFiles $spotWorkerCustomScriptExtensionFiles

if ($mainParameters.spotWorkerAdminPassword -eq "") {
    Write-Host "Setting spotWorkerAdminPassword to a random value"
    $mainParameters.spotWorkerAdminPassword = Get-RandomPassword
}

New-Deployment `
    -ResourceGroupName $ResourceSettings.ResourceGroupName `
    -DeploymentDir $DeploymentDir `
    -DeploymentLabel $DeploymentLabel `
    -DeploymentName "main" `
    -BicepPath "../bicep/main.bicep" `
    -Parameters $mainParameters

Remove-AzRoleAssignmentWithRetry $currentUser $ResourceSettings.ResourceGroupName "Storage Blob Data Contributor" -AllowMissing

if (!$SkipPrepare) {
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
                if ($attempt -eq 24) {
                    Write-Host "$url is still not ready... we'll keep trying!"
                }

                if ($attempt -lt 120 -and $_.Exception.Response.StatusCode -ge 500) {
                    Start-Sleep -Seconds 5
                    continue
                }
                else {
                    throw
                }
            }
        }
    }
}

Write-Status "Deployment is complete."
Write-Host "Go to here for the admin panel: $(Get-AppServiceBaseUrl $ResourceSettings.WebsiteName)/Admin"
