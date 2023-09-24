#Requires -Modules Az.Accounts

[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [ValidateSet("win-x64", "linux-x64")]
    [string]$RuntimeIdentifier,

    [Parameter(Mandatory = $false)]
    [string]$StampName,

    [Parameter(Mandatory = $false)]
    [switch]$AllowDeployUser,

    [Parameter(Mandatory = $false)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $false)]
    [string]$WorkerZipPath,

    [Parameter(Mandatory = $false)]
    [string]$AzureFunctionsHostZipPath
)

dynamicparam {
    Import-Module (Join-Path $PSScriptRoot "scripts/NuGet.Insights.psm1")
    
    $ConfigNameKey = "ConfigName"
    $configNameParameter = Get-ConfigNameDynamicParameter ([string]) $ConfigNameKey

    $parameterDictionary = [System.Management.Automation.RuntimeDefinedParameterDictionary]::new()
    $parameterDictionary.Add($ConfigNameKey, $configNameParameter)
    return $parameterDictionary
}

begin {
    $ConfigName = $PsBoundParameters[$ConfigNameKey]
}

process {
    Import-Module (Join-Path $PSScriptRoot "scripts/NuGet.Insights.psm1")

    $RuntimeIdentifier = Get-DefaultRuntimeIdentifier $RuntimeIdentifier
    $resourceSettings = Get-ResourceSettings $ConfigName $StampName $RuntimeIdentifier
    
    # Optionally, add the current (deploying) user as an allowed user for the website admin panel.
    if ($AllowDeployUser) {
        $context = Get-AzContext
        $homeAccountId = $context.Account.ExtendedProperties.HomeAccountId
        if (!$homeAccountId) {
            throw "Could not find the 'HomeAccountId' from (Get-AzContext).Account.ExtendedProperties.HomeAccountId. Did you run Connect-AzAccount?"
        }
        else {
            if (!$resourceSettings.WebsiteConfig['NuGetInsights'].AllowedUsers) {
                $resourceSettings.WebsiteConfig['NuGetInsights'].AllowedUsers = @()
            }
    
            $objectId, $tenantId = $homeAccountId.Split('.', 2)
            Write-Status "Adding allowed user:"
            Write-Status "  Tenant ID: $tenantId"
            Write-Status "  Object ID: $objectId"
    
            $resourceSettings.WebsiteConfig['NuGetInsights'].AllowedUsers += @{
                TenantId = $tenantId;
                ObjectId = $objectId
            }
        }
    }
    
    
    # Publish (build and package) the app code
    $deploymentDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../artifacts/deploy"))
    function Publish-Project ($ProjectName) {
        Write-Status "Publishing project '$ProjectName'..."

        # Workaround: https://github.com/Azure/azure-functions-dotnet-worker/issues/1834
        dotnet build (Join-Path $PSScriptRoot "../src/$ProjectName") `
            --configuration Release `
            --runtime $RuntimeIdentifier `
            --self-contained false | Out-Default
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build $ProjectName."
        }

        $publishDir = Join-Path $deploymentDir $ProjectName
        if (Test-Path $publishDir) {
            Remove-Item $publishDir -Force -Recurse
        }

        dotnet publish (Join-Path $PSScriptRoot "../src/$ProjectName") `
            --no-build `
            --configuration Release `
            --runtime $RuntimeIdentifier `
            --self-contained false `
            --output $publishDir | Out-Default
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish $ProjectName."
        }
        
        $zipPath = Join-Path $deploymentDir "$ProjectName.zip"
        Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

        Remove-Item $publishDir -Recurse -Force
    
        return $zipPath.ToString()
    }
    
    if (!$WebsiteZipPath) { $WebsiteZipPath = Publish-Project "Website" }
    if (!$WorkerZipPath) { $WorkerZipPath = Publish-Project "Worker" }
    if (!$AzureFunctionsHostZipPath -and $resourceSettings.UseSpotWorkers) {
        if (!(Test-Path $deploymentDir)) { New-Item $deploymentDir -ItemType Directory | Out-Null }
        Write-Status "Publishing Azure Functions Host..."
        $AzureFunctionsHostZipPath = Join-Path $deploymentDir "AzureFunctionsHost.zip"
        . (Join-Path $PSScriptRoot "build-host.ps1") `
            -RuntimeIdentifier $RuntimeIdentifier `
            -OutputPath $AzureFunctionsHostZipPath
    }
    
    $parameters = @{
        ResourceSettings          = $resourceSettings;
        DeploymentDir             = $deploymentDir;
        WebsiteZipPath            = $WebsiteZipPath;
        WorkerZipPath             = $WorkerZipPath;
        AzureFunctionsHostZipPath = $AzureFunctionsHostZipPath;
        RuntimeIdentifier         = $RuntimeIdentifier;
    }
    
    Write-Status "Using the following deployment parameters:"
    ConvertTo-Json $parameters -Depth 100 | Out-Default
    
    Approve-SubscriptionId $resourceSettings.SubscriptionId
    
    . (Join-Path $PSScriptRoot "scripts/Invoke-Deploy.ps1") @parameters    
}
