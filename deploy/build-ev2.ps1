[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [ValidateSet("win-x64", "linux-x64")]
    [string]$RuntimeIdentifier,

    [Parameter(Mandatory = $true)]
    [string]$BuildVersion,

    [Parameter(Mandatory = $true)]
    [string]$WebsiteZipPath,

    [Parameter(Mandatory = $true)]
    [string]$WorkerZipPath,

    [Parameter(Mandatory = $false)]
    [string]$AzureFunctionsHostZipPath
)

dynamicparam {
    Import-Module (Join-Path $PSScriptRoot "scripts/NuGet.Insights.psm1") -Force

    $ConfigNamesKey = "ConfigNames"
    $configNamesParameter = Get-ConfigNameDynamicParameter ([string[]]) $ConfigNamesKey

    $parameterDictionary = [System.Management.Automation.RuntimeDefinedParameterDictionary]::new()
    $parameterDictionary.Add($ConfigNamesKey, $configNamesParameter)
    return $parameterDictionary
}

begin {
    $ConfigNames = $PsBoundParameters[$ConfigNamesKey]
}

process {
    function New-ServiceModelFileINT($resourceSettingsByConfigName, $environmentName) {
        # Docs: https://ev2docs.azure.net/features/service-artifacts/servicemodel.html
        # Region-agnostic schema: https://ev2schema.azure.net/schemas/2020-04-01/regionAgnosticServiceModel.json

        # Infer shared values from the first config
        $first = $resourceSettingsByConfigName.Values | Select-Object -First 1
        if (-not $first) {
            throw "No resource settings were provided; cannot generate ServiceModel.INT.json."
        }

        $serviceIdentifier = $first.ServiceTreeId
        $tenantId = $first.TenantId
        if (-not $tenantId) {
            throw "TenantId is required for region-agnostic ServiceModel.INT.json. Add it to your config so Get-ResourceSettings returns ResourceSettings.TenantId."
        }

        # Ensure all configs share the same service tree ID (service identifier)
        $distinctServiceTreeIds = $resourceSettingsByConfigName.Values | Select-Object -ExpandProperty ServiceTreeId -Unique
        if (($distinctServiceTreeIds | Measure-Object).Count -ne 1) {
            throw "All configs must have the same ServiceTreeId to generate a single ServiceModel.INT.json. Found: $($distinctServiceTreeIds -join ', ')."
        }

        $serviceResourceGroupDefinitions = @()

        foreach ($resourceSettings in ($resourceSettingsByConfigName.Values | Sort-Object ConfigName)) {
            # Expect INT stamps like:
            # - nuget-int-usnc
            # - nuget-int-cus
            $configName = $resourceSettings.ConfigName
            $stamp = $resourceSettings.StampName

            if ($configName -ne $stamp) {
                throw "The config name must match the stamp name."
            }

            # Convert location string to EV2 region token
            # "North Central US" -> "NorthCentralUS"
            # "Central US"       -> "CentralUS"
            $regionToken = ($resourceSettings.Location -replace '\s', '')
            if (-not $regionToken) {
                throw "Could not derive region token from Location '$($resourceSettings.Location)' for config '$configName'."
            }

            # Friendly name used by the sample you provided:
            # "NuGetIntUsncInsights" / "NuGetIntCusInsights"
            # We'll derive it from the part after "nuget-int-" (e.g., usnc, cus)
            $suffix = $configName
            if ($suffix -match '^nuget-int-(.+)$') {
                $suffix = $Matches[1]
            }
            $suffixTitle = ($suffix.Substring(0, 1).ToUpperInvariant() + $suffix.Substring(1).ToLowerInvariant())
            $rgDefinitionName = "NuGetInt$($suffixTitle)Insights"

            $serviceResourceGroupDefinitions += [ordered]@{
                name                  = $rgDefinitionName
                azureResourceGroupName = $resourceSettings.ResourceGroupName
                subscriptionKey       = "NuGetServiceInt"
                executionConstraint   = [ordered]@{
                    quantifier = "Always"
                    level      = "Region"
                    regions    = @($regionToken)
                }
                serviceResourceDefinitions = @(
                    [ordered]@{
                        name       = $storageServiceResourceName
                        composedOf = [ordered]@{
                            arm = [ordered]@{
                                templatePath   = "Templates\storage.Template.json"
                                parametersPath = "Parameters\$configName.storage.Parameters.json"
                            }
                        }
                    }
                    [ordered]@{
                        name       = $copyServiceResourceName
                        composedOf = [ordered]@{
                            extension = [ordered]@{
                                rolloutParametersPath = "Parameters\$configName.copy.RolloutParameters.json"
                            }
                        }
                    }
                    [ordered]@{
                        name       = $mainServiceResourceName
                        composedOf = [ordered]@{
                            arm = [ordered]@{
                                templatePath   = "Templates\main.Template.json"
                                parametersPath = "Parameters\$configName.main.Parameters.json"
                            }
                        }
                    }
                )
            }
        }

        # subscriptionProvisioning.RolloutParameters.INT.json (new)
        $subscriptionProvisioningRolloutParametersPath = Join-Path $ev2 "Parameters\subscriptionProvisioning.RolloutParameters.INT.json"

        # NOTE: this file is separate, but ServiceModel references it.
        # We'll generate it from the first config's subscription and a fixed display name/workload.
        # This matches the sample you provided.
        New-SubscriptionProvisioningRolloutParametersFileINT `
            -ResourceSettings $first `
            -FilePath $subscriptionProvisioningRolloutParametersPath

        $serviceModel = [ordered]@{
            "`$schema"        = "https://ev2schema.azure.net/schemas/2020-04-01/regionAgnosticServiceModel.json"
            contentVersion    = "1.0.0.0"
            serviceMetadata   = [ordered]@{
                serviceIdentifier = $serviceIdentifier
                serviceGroup      = "Microsoft.DevDiv.NugetService.NuGet.Insights"
                environment       = $environmentName.ToUpperInvariant()
                displayName       = "NuGet.Insights - $($environmentName.ToUpperInvariant())"
                tenantId          = $tenantId
            }
            subscriptionProvisioning = [ordered]@{
                rolloutParametersPath = "Parameters\subscriptionProvisioning.RolloutParameters.$($environmentName.ToUpperInvariant()).json"
            }
            serviceResourceGroupDefinitions = $serviceResourceGroupDefinitions
        }

        $serviceModelPath = Join-Path $ev2 "ServiceModels\ServiceModel.$($environmentName.ToUpperInvariant()).json"
        $dirPath = Split-Path $serviceModelPath
        if (!(Test-Path $dirPath)) {
            New-Item $dirPath -ItemType Directory | Out-Null
        }

        $serviceModel | ConvertTo-Json -Depth 100 | Format-Json | Out-File $serviceModelPath -Encoding UTF8
    }

    function New-RolloutSpecFileINT($environmentName, $resourceSettingsByConfigName) {
        # Region-agnostic schema: https://ev2schema.azure.net/schemas/2020-04-01/RegionAgnosticRolloutSpecification.json

        $envUpper = $environmentName.ToUpperInvariant()

        # Step ordering: explicitly put USNC first then CUS if present, to match the sample.
        # Otherwise, fall back to lexicographic config name.
        $preferredOrder = @(
            "nuget-int-usnc",
            "nuget-int-cus"
        )

        $configs = $resourceSettingsByConfigName.Values
        $ordered = @()

        foreach ($p in $preferredOrder) {
            $match = $configs | Where-Object { $_.ConfigName -eq $p }
            if ($match) { $ordered += $match }
        }

        $remaining = $configs | Where-Object { $preferredOrder -notcontains $_.ConfigName } | Sort-Object ConfigName
        $ordered += $remaining

        $steps = @()

        foreach ($resourceSettings in $ordered) {
            $configName = $resourceSettings.ConfigName
            $suffix = $configName
            if ($suffix -match '^nuget-int-(.+)$') {
                $suffix = $Matches[1]
            }
            $suffixUpper = $suffix.ToUpperInvariant()

            $deployStorageName = "DeployStorage$suffixUpper"
            $copyName = "Copy$suffixUpper"
            $deployMainName = "DeployMain$suffixUpper"

            $steps += [ordered]@{
                name       = $deployStorageName
                targetType = "ServiceResource"
                targetName = $storageServiceResourceName
                actions    = @("deploy")
            }
            $steps += [ordered]@{
                name       = $copyName
                targetType = "ServiceResource"
                targetName = $copyServiceResourceName
                actions    = @("extension/AzCopy")
                dependsOn  = @($deployStorageName)
            }
            $steps += [ordered]@{
                name       = $deployMainName
                targetType = "ServiceResource"
                targetName = $mainServiceResourceName
                actions    = @("deploy")
                dependsOn  = @($copyName)
            }
        }

        $rolloutSpec = [ordered]@{
            "`$schema"      = "https://ev2schema.azure.net/schemas/2020-04-01/RegionAgnosticRolloutSpecification.json"
            contentVersion  = "1.0.0.0"
            rolloutMetadata = [ordered]@{
                serviceModelPath = "ServiceModels\ServiceModel.$envUpper.json"
                name             = "NuGet.Insights $BuildVersion"
                rolloutType      = "Major"
                buildSource      = [ordered]@{
                    parameters = [ordered]@{
                        versionFile = "BuildVer.txt"
                    }
                }
                notification     = [ordered]@{
                    email = [ordered]@{
                        to      = "nugetservereng@microsoft.com"
                        options = [ordered]@{
                            when = @("onError")
                        }
                    }
                }
            }
            orchestratedSteps = $steps
        }

        $rolloutSpecPath = Join-Path $ev2 "RolloutSpec.$envUpper.json"
        $rolloutSpec | ConvertTo-Json -Depth 100 | Format-Json | Out-File $rolloutSpecPath -Encoding UTF8
    }

    function New-SubscriptionProvisioningRolloutParametersFileINT {
        [CmdletBinding()]
        param(
            [Parameter(Mandatory = $true)]
            $ResourceSettings,

            [Parameter(Mandatory = $true)]
            [string]$FilePath
        )

        # Matches the sample you provided.
        $rolloutParameters = [ordered]@{
            "`$schema"     = "https://ev2schema.azure.net/schemas/2020-01-01/rolloutParameters.json"
            contentVersion = "1.0.0.0"
            subscriptions  = @(
                [ordered]@{
                    name                   = "subscriptionProvisioning"
                    displayName            = "NuGet Staging (Internal Consumption)"
                    isServiceScope         = "True"
                    backfilledSubscriptionId = $ResourceSettings.SubscriptionId
                    workload               = "Production"
                }
            )
        }

        $dirPath = Split-Path $FilePath
        if (!(Test-Path $dirPath)) {
            New-Item $dirPath -ItemType Directory | Out-Null
        }

        $rolloutParameters | ConvertTo-Json -Depth 100 | Format-Json | Out-File $FilePath -Encoding UTF8
    }

    function New-RolloutParametersFile($ResourceSettings, $FilePath, $DeploymentBaseUrl) {
        # Existing (per-stamp) AzCopy rollout parameters (kept as-is).
        # Docs: https://ev2docs.azure.net/features/service-artifacts/rolloutparameters.html
        # Docs: https://msazure.visualstudio.com/One/_wiki/wikis/One.wiki/51808/AzCopy-Ev2-Extension
        $rolloutParameters = [ordered]@{
            "`$schema"     = "https://ev2schema.azure.net/schemas/2020-01-01/rolloutParameters.json";
            contentVersion = "1.0.0.0";
            extensions     = @(
                [ordered]@{
                    name                 = "AzCopy";
                    type                 = "Microsoft.Storage/AzCopy";
                    version              = "2020-07-17";
                    connectionProperties = $ResourceSettings.Ev2AzCopyConnectionProperties;
                    payloadProperties    = [ordered]@{
                        sourceSAS          = [ordered]@{
                            reference = [ordered]@{
                                path        = "bin";
                                isDirectory = "true"
                            }
                        };
                        destinationSAS     = [ordered]@{
                            value = $DeploymentBaseUrl
                        };
                        DestinationService = [ordered]@{
                            value = "blob"
                        };
                        AsSubdir           = [ordered]@{
                            value = "false"
                        }
                    }
                }
            )
        }

        $dirPath = Split-Path $FilePath
        if (!(Test-Path $dirPath)) {
            New-Item $dirPath -ItemType Directory | Out-Null
        }

        $rolloutParameters | ConvertTo-Json -Depth 100 | Format-Json | Out-File $FilePath -Encoding UTF8
    }

    function New-Bicep($name) {
        $bicepPath = Join-Path $PSScriptRoot "bicep/$name.bicep"
        $templatePath = Join-Path $ev2 (Get-TemplatePath $name)

        $templatesDir = Split-Path $templatePath
        if (!(Test-Path $templatesDir)) {
            New-Item $templatesDir -ItemType Directory | Out-Null
        }

        $bicepExe, $bicepArgs = Get-Bicep
        & $bicepExe @bicepArgs $bicepPath --outfile $templatePath
        if ($LASTEXITCODE -ne 0) {
            throw "Command 'bicep build' failed with exit code $LASTEXITCODE."
        }
    }

    function Get-RolloutParametersPath($configName, $name) {
        return "Parameters/$configName.$name.RolloutParameters.json"
    }

    function Get-ParametersPath($configName, $templateName) {
        return "Parameters/$configName.$templateName.Parameters.json"
    }

    function Get-TemplatePath($name) {
        return "Templates/$name.Template.json"
    }

    $RuntimeIdentifier = Get-DefaultRuntimeIdentifier $RuntimeIdentifier

    # Declare shared variables
    $artifacts = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../artifacts"))
    $ev2 = Join-Path $artifacts "ExpressV2"
    $bin = Join-Path $ev2 "bin"
    $storageServiceResourceName = "Storage.ResourceInstance"
    $copyServiceResourceName = "Copy.ResourceInstance"
    $mainServiceResourceName = "Main.ResourceInstance"

    $websiteZipFile = "Website.zip"
    $workerZipFile = "Worker.zip"
    $azureFunctionsHostZipFile = "AzureFunctionsHost.zip"
    $workerStandaloneEnvFilePattern = "WorkerStandalone.{0}.env"
    $installWorkerStandaloneScriptFile = "Install-WorkerStandalone.ps1"
    $dotnetInstallScriptFile = "dotnet-install.ps1"

    $scriptsToCopy = [ordered]@{
        "scripts/Install-WorkerStandalone.ps1" = (Join-Path $bin $installWorkerStandaloneScriptFile);
        "scripts/NuGet.Insights.psm1"          = (Join-Path $ev2 "NuGet.Insights.psm1");
        "scripts/Set-DeploymentParameters.ps1" = (Join-Path $ev2 "Set-DeploymentParameters.ps1");
    }

    # Install Bicep, if needed.
    if (!(Get-Command bicep -CommandType Application -ErrorAction Ignore)) {
        Write-Host "Installing Bicep..."
        # Source: https://github.com/Azure/bicep/blob/main/docs/installing.md#manual-with-powershell
        if ($IsLinux) {
            curl -Lo bicep.bin https://github.com/Azure/bicep/releases/latest/download/bicep-linux-x64
            chmod +x ./bicep.bin
            sudo mv ./bicep.bin /usr/local/bin/bicep
        }
        elseif ($IsMacOS) {
            curl -Lo bicep https://github.com/Azure/bicep/releases/latest/download/bicep-osx-x64
            chmod +x ./bicep
            sudo spctl --add ./bicep
            sudo mv ./bicep /usr/local/bin/bicep
        }
        else {
            $installPath = "$env:USERPROFILE\.bicep"
            $installDir = New-Item -ItemType Directory -Path $installPath -Force
            $installDir.Attributes += 'Hidden'
            (New-Object Net.WebClient).DownloadFile("https://github.com/Azure/bicep/releases/latest/download/bicep-win-x64.exe", "$installPath\bicep.exe")
            $currentPath = (Get-Item -path "HKCU:\Environment" ).GetValue('Path', '', 'DoNotExpandEnvironmentNames')
            if (-not $currentPath.Contains("%USERPROFILE%\.bicep")) { setx PATH ($currentPath + ";%USERPROFILE%\.bicep") }
            if (-not $env:path.Contains($installPath)) { $env:path += ";$installPath" }
        }
    }

    if (Test-Path $ev2) {
        Remove-Item $ev2 -Recurse -Force
    }

    # Compile the Bicep templates to raw ARM JSON.
    New-Bicep "storage"
    New-Bicep "main"

    $bin = Join-Path $ev2 "bin"
    New-Item $bin -ItemType Directory | Out-Null

    # Build the Ev2 artifacts (per-config parameter files are still produced)
    $anyUseSpotWorkers = $false

    # We'll collect resource settings so we can produce a single ServiceModel.INT.json and RolloutSpec.INT.json
    $resourceSettingsByConfigName = @{}
    $environmentName = $null

    foreach ($configName in $ConfigNames) {
        $resourceSettings = Get-ResourceSettings $configName $null $RuntimeIdentifier

        if (-not $environmentName) {
            $environmentName = $resourceSettings.EnvironmentName
        }
        elseif ($environmentName -ne $resourceSettings.EnvironmentName) {
            throw "All configs must have the same EnvironmentName to generate a single set of INT artifacts. Found '$environmentName' and '$($resourceSettings.EnvironmentName)'."
        }

        if ($resourceSettings.ConfigName -ne $resourceSettings.StampName) {
            throw "The config name must match the stamp name."
        }
        if (!$resourceSettings.SubscriptionId) {
            $configPath = Get-ConfigPath $resourceSettings.ConfigName
            throw "A subscription ID is required for generating Ev2 artifacts. Specify a value in file $configPath at JSON path $.Deployment.SubscriptionId."
        }
        if (!$resourceSettings.ServiceTreeId) {
            $configPath = Get-ConfigPath $resourceSettings.ConfigName
            throw "A ServiceTree ID is required for generating Ev2 artifacts. Specify a value in file $configPath at JSON path $.Deployment.ServiceTreeId."
        }
        if (!$resourceSettings.EnvironmentName) {
            $configPath = Get-ConfigPath $resourceSettings.ConfigName
            throw "A environment name is required for generating Ev2 artifacts. Specify a value in file $configPath at JSON path $.Deployment.EnvironmentName."
        }
        if (!$resourceSettings.WebsiteAadAppClientId) {
            $configPath = Get-ConfigPath $resourceSettings.ConfigName
            throw "A website AAD client ID is required for generating Ev2 artifacts. You can use the prepare.ps1 script to create the AAD app registration for the first time. Specify a value in file $configPath at JSON path $.deployment.WebsiteAadAppClientId."
        }

        $pathReferences = @(
            "websiteZipUrl"
            "workerZipUrl"
        )

        $deploymentBaseUrl = "https://$($resourceSettings.StorageAccountName).blob.core.windows.net/$($resourceSettings.DeploymentContainerName)/$BuildVersion"
        $workerZipUrl = "$deploymentBaseUrl/$workerZipFile"
        $spotWorkerCustomScriptExtensionFiles = @()

        if ($resourceSettings.UseSpotWorkers) {
            $workerStandaloneEnv = New-WorkerStandaloneEnv $resourceSettings
            $workerStandaloneEnvFile = $workerStandaloneEnvFilePattern -f $resourceSettings.ConfigName
            $workerStandaloneEnv | Out-EnvFile -FilePath (Join-Path $bin $workerStandaloneEnvFile)

            $spotWorkerCustomScriptExtensionFiles = @(
                $workerZipUrl,
                "$deploymentBaseUrl/$azureFunctionsHostZipFile",
                "$deploymentBaseUrl/$dotnetInstallScriptFile",
                "$deploymentBaseUrl/$workerStandaloneEnvFile",
                "$deploymentBaseUrl/$installWorkerStandaloneScriptFile"
            )
        }

        $storageParameters = New-StorageParameters `
            -ResourceSettings $resourceSettings `
            -DenyTraffic $false `
            -AllowSharedKeyAccess $false

        $mainParameters = New-MainParameters `
            -ResourceSettings $resourceSettings `
            -DeploymentLabel "PLACEHOLDER" `
            -WebsiteZipUrl (Join-Path "bin" $websiteZipFile) `
            -WorkerZipUrl (Join-Path "bin" $workerZipFile) `
            -SpotWorkerCustomScriptExtensionFiles $spotWorkerCustomScriptExtensionFiles

        $storageParametersPath = Join-Path $ev2 (Get-ParametersPath $resourceSettings.ConfigName "storage")
        $rolloutParametersPath = Join-Path $ev2 (Get-RolloutParametersPath $ResourceSettings.ConfigName "copy")
        $mainParametersPath = Join-Path $ev2 (Get-ParametersPath $resourceSettings.ConfigName "main")

        New-ParameterFile $storageParameters @() $storageParametersPath
        New-RolloutParametersFile $resourceSettings $rolloutParametersPath $deploymentBaseUrl
        New-ParameterFile $mainParameters $pathReferences $mainParametersPath

        $resourceSettingsByConfigName[$resourceSettings.ConfigName] = $resourceSettings
        $anyUseSpotWorkers = $anyUseSpotWorkers -or $resourceSettings.UseSpotWorkers
    }

    if (-not $environmentName) {
        throw "No configurations were provided."
    }

    # NEW: generate consolidated region-agnostic INT artifacts
    New-ServiceModelFileINT -resourceSettingsByConfigName $resourceSettingsByConfigName -environmentName $environmentName
    New-RolloutSpecFileINT -environmentName $environmentName -resourceSettingsByConfigName $resourceSettingsByConfigName

    $BuildVersion | Out-File (Join-Path $ev2 "BuildVer.txt") -NoNewline -Encoding UTF8

    # Copy the runtime assets
    Copy-Item $WebsiteZipPath -Destination (Join-Path $bin $websiteBinPath) -Verbose
    Copy-Item $WorkerZipPath -Destination (Join-Path $bin $workerBinPath) -Verbose

    if ($AzureFunctionsHostZipPath) {
        Copy-Item $AzureFunctionsHostZipPath -Destination (Join-Path $bin $azureFunctionsHostBinPath) -Verbose

        $dotnetInstallScriptPath = Join-Path $bin "dotnet-install.ps1"
        Invoke-DownloadDotnetInstallScript $dotnetInstallScriptPath
    }
    elseif ($anyUseSpotWorkers) {
        throw "No AzureFunctionsHostZipPath parameter was provided but at least one of the configurations has UseSpotWorkers set to true."
    }

    foreach ($pair in $scriptsToCopy.GetEnumerator()) {
        $source = Join-Path $PSScriptRoot $pair.Key
        Copy-Item -Path $source -Destination $pair.Value -Verbose
    }

    Write-Host "Wrote Ev2 files to: $ev2"
}
