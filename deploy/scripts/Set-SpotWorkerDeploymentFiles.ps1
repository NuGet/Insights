[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ManagedIdentityClientId,

    [Parameter(Mandatory = $true)]
    [string]$DeploymentLabel,
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountName,
    
    [Parameter(Mandatory = $true)]
    [string]$SpotWorkerDeploymentContainerName
)

$ErrorActionPreference = "Stop"

Write-Output "Connecting to Azure with client ID $ManagedIdentityClientId"
Connect-AzAccount -Identity -AccountId $ManagedIdentityClientId | Out-Null
Write-Output "Setting up context for storage account $StorageAccountName"
$storageContext = New-AzStorageContext -StorageAccountName $StorageAccountName -UseConnectedAccount

$urls = @()

Write-Output "Reading list of supporting files"
$deploymentFiles = Get-Content -Path "./$env:AZ_SCRIPTS_PATH_SUPPORTING_SCRIPT_URI_FILE_NAME" | `
    ForEach-Object { ([Uri]$_).Segments[-1] }

foreach ($fileName in $deploymentFiles) {
    $blobName = "$DeploymentLabel/$fileName"

    Write-Output "Uploading $fileName"
    
    $attempt = 0
    while ($true) {
        $attempt++

        try {
            $blob = Set-AzStorageBlobContent `
                -Context $storageContext `
                -Container $SpotWorkerDeploymentContainerName `
                -File "./$fileName" `
                -Blob $blobName `
                -Confirm:$false `
                -ErrorAction Stop
            break
        }
        catch {
            if ($attempt -lt 60 -and $_.Exception.RequestInformation.HttpStatusCode -eq 403) {
                Write-Warning "Attempt $($attempt) - HTTP 403 Forbidden. Trying again in 10 seconds."
                Start-Sleep 10
                continue
            }
            else {
                
                throw
            }
        }
    }

    $url = $blob.BlobClient.Uri.AbsoluteUri
    Write-Output "Uploaded blob to $url"
    $urls += @($url)
}

$DeploymentScriptOutputs = @{ customScriptExtensionFiles = $urls }
