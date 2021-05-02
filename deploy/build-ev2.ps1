using module "scripts/ExplorePackages.psm1"
using namespace ExplorePackages

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string[]]$ConfigNames,

    [Parameter(Mandatory = $true)]
    [string]$BuildVersion,

    [Parameter(Mandatory = $true)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $true)]
    [string]$WorkerZipPath
)

function New-ServiceModelFile($resourceSettings) {
    # Docs: https://ev2docs.azure.net/getting-started/authoring/service-model/servicemodel.html
    $definitionName = "Deploy.ServiceDefinition"
    $resourceName = "Deploy.ResourceDefinition"
    $serviceModel = [ordered]@{
        "`$schema"                      = "http://schema.express.azure.com/schemas/2015-01-01-alpha/ServiceModel.json";
        contentVersion                  = "0.0.0.1";
        serviceMetadata                 = [ordered]@{
            serviceGroup      = "ExplorePackages";
            environment       = $resourceSettings.EnvironmentName;
            serviceIdentifier = $resourceSettings.ServiceTreeId;
        };
        serviceResourceGroupDefinitions = @(
            [ordered]@{
                name                       = $definitionName;
                serviceResourceDefinitions = @(
                    [ordered]@{
                        name       = $resourceName;
                        composedOf = [ordered]@{
                            arm = [ordered]@{
                                templatePath = Get-TemplatePath "main";
                                parameters   = Get-ParametersPath $resourceSettings.ConfigName;
                            }
                        }
                    }
                )
            }
        );
        serviceResourceGroups           = @(
            [ordered]@{
                name                   = "Deploy.ServiceInstance";
                azureResourceGroupName = $resourceSettings.ResourceGroupName;
                location               = $resourceSettings.Location;
                instanceOf             = $definitionName;
                azureSubscriptionId    = $resourceSettings.SubscriptionId;
                serviceResources       = @(
                    [ordered]@{
                        name       = $serviceResourceName;
                        instanceOf = $resourceName;
                    }
                );
            }
        )
    }

    $serviceModelPath = Get-ServiceModelPath $resourceSettings.ConfigName
    $serviceModelPath = Join-Path $ev2 $serviceModelPath
    $dirPath = Split-Path $serviceModelPath
    if (!(Test-Path $dirPath)) {
        New-Item $dirPath -ItemType Directory | Out-Null
    }

    $serviceModel | ConvertTo-Json -Depth 100 | Out-File $serviceModelPath -Encoding UTF8
}

function New-RolloutSpecFile($resourceSettings) {
    # Docs: https://ev2docs.azure.net/getting-started/authoring/rollout-spec/rolloutspec.html
    $rolloutSpec = [ordered]@{
        "`$schema"        = "http://schema.express.azure.com/schemas/2015-01-01-alpha/RolloutSpec.json";
        contentVersion    = "1.0.0.0";
        rolloutMetadata   = [ordered]@{
            serviceModelPath = Get-ServiceModelPath $resourceSettings.ConfigName
            name             = "ExplorePackages-$($resourceSettings.EnvironmentName)"
            rolloutType      = "Major";
            buildSource      = [ordered]@{
                parameters = [ordered]@{
                    versionFile = "BuildVer.txt"
                }
            }
        };
        orchestratedSteps = @(
            [ordered]@{
                name       = "Deploy.OrchestratedStep";
                targetType = "ServiceResource";
                targetName = $serviceResourceName;
                actions    = @( "deploy" );
                dependsOn  = @();
            }
        )
    }

    $rolloutSpecPath = Get-RolloutSpecPath $resourceSettings.ConfigName
    $rolloutSpecPath = Join-Path $ev2 $rolloutSpecPath
    $dirPath = Split-Path $rolloutSpecPath
    if (!(Test-Path $dirPath)) {
        New-Item $dirPath -ItemType Directory | Out-Null
    }

    $rolloutSpec | ConvertTo-Json -Depth 100 | Out-File $rolloutSpecPath -Encoding UTF8
}

function New-Bicep($name) {
    $bicepPath = Join-Path $PSScriptRoot "$name.bicep"
    $templatePath = Join-Path $ev2 (Get-TemplatePath $name)

    $templatesDir = Split-Path $templatePath
    if (!(Test-Path $templatesDir)) {
        New-Item $templatesDir -ItemType Directory | Out-Null
    }

    bicep build $bicepPath --outfile $templatePath

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $($LASTEXITCODE): bicep build $bicepPath --outfile $templatePath"
    }
}

function Get-ServiceModelPath($configName) {
    return "ServiceModels/$configName.ServiceModel.json"
}

function Get-RolloutSpecPath($configName) {
    return "$configName.RolloutSpec.json"
}

function Get-ParametersPath($configName) {
    return "Parameters/$configName.Parameters.json"
}

function Get-TemplatePath($name) {
    return "Templates/$name.Template.json"
}

# Declare shared variables
$ev2 = Join-Path $PSScriptRoot "../artifacts/ExpressV2"
$serviceResourceName = "Deploy.ResourceInstance"
$websiteBinPath = "bin/Website.zip"
$workerBinPath = "bin/Worker.zip"

# Compile the Bicep templates to raw ARM JSON.
New-Bicep "main"
New-Bicep "storage-and-kv"

# Build the Ev2 artifacts
foreach ($configName in $ConfigNames) {
    $resourceSettings = Get-ResourceSettings $configName

    if ($resourceSettings.ConfigName -ne $resourceSettings.StampName) {
        throw "The config name must match the stamp name."
    }
    if (!$resourceSettings.SubscriptionId) {
        $configPath = Get-ConfigPath $resourceSettings.ConfigName
        throw "A subscription ID is required for generating Ev2 artifacts. Specify a value in file $configPath at JSON path $.deployment.SubscriptionId."
    }
    if (!$resourceSettings.ServiceTreeId) {
        $configPath = Get-ConfigPath $resourceSettings.ConfigName
        throw "A ServiceTree ID is required for generating Ev2 artifacts. Specify a value in file $configPath at JSON path $.deployment.ServiceTreeId."
    }
    if (!$resourceSettings.EnvironmentName) {
        $configPath = Get-ConfigPath $resourceSettings.ConfigName
        throw "A environment name is required for generating Ev2 artifacts. Specify a value in file $configPath at JSON path $.deployment.EnvironmentName."
    }
    if (!$resourceSettings.WebsiteAadAppClientId) {
        $configPath = Get-ConfigPath $resourceSettings.ConfigName
        throw "A website AAD client ID is required for generating Ev2 artifacts. You can use the prepare.ps1 script to create the AAD app registration for the first time. Specify a value in file $configPath at JSON path $.deployment.WebsiteAadAppClientId."
    }

    $parameters = New-MainParameters $resourceSettings $websiteBinPath $workerBinPath
    $parametersPath = Join-Path $ev2 (Get-ParametersPath $resourceSettings.ConfigName)
    New-ParameterFile $parameters @("websiteZipUrl", "workerZipUrl") $parametersPath
    New-ServiceModelFile $resourceSettings
    New-RolloutSpecFile $resourceSettings
}

$BuildVersion | Out-File (Join-Path $ev2 "BuildVer.txt") -NoNewline -Encoding UTF8


# Copy the binaries
$bin = Join-Path $ev2 "bin"
if (!(Test-Path $bin)) {
    New-Item $bin -ItemType Directory | Out-Null
}
Copy-Item $WebsiteZipPath -Destination (Join-Path $ev2 $websiteBinPath)
Copy-Item $WorkerZipPath -Destination (Join-Path $ev2 $workerBinPath)

Write-Host "Wrote Ev2 files to: $ev2"
