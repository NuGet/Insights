class ResourceSettings {
    [ValidateNotNullOrEmpty()]
    [string]$ConfigName

    [ValidateNotNullOrEmpty()]
    [string]$StampName

    [ValidateNotNullOrEmpty()]
    [string]$Location
    
    [ValidateNotNullOrEmpty()]
    [string]$LogAnalyticsWorkspaceName

    [ValidateNotNullOrEmpty()]
    [string]$AppInsightsName
    
    [ValidateNotNullOrEmpty()]
    [ValidateRange(0, 1000)]
    [int]$AppInsightsDailyCapGb
    
    [ValidateNotNullOrEmpty()]
    [string]$ActionGroupName
    
    [ValidateNotNullOrEmpty()]
    [ValidateLength(1, 12)]
    [string]$ActionGroupShortName
    
    [ValidateNotNullOrEmpty()]
    [string]$WebsitePlanName
    
    [ValidateNotNullOrEmpty()]
    [string]$WebsiteName
    
    [ValidateNotNullOrEmpty()]
    [string]$WorkerPlanNamePrefix
    
    [ValidateNotNullOrEmpty()]
    [string]$WorkerAutoscaleNamePrefix
    
    [ValidateNotNullOrEmpty()]
    [string]$WorkerHostId
    
    [ValidateNotNullOrEmpty()]
    [string]$WorkerNamePrefix
    
    [ValidateNotNullOrEmpty()]
    [string]$UserManagedIdentityName
    
    [ValidateSet("Y1", "B1", "B2", "B3", "S1", "S2", "S3", "P1v2", "P2v2", "P3v2", "P1v3", "P2v3", "P3v3")]
    [string]$WorkerSku
    
    [ValidateRange(1, 30)]
    [ValidateNotNullOrEmpty()]
    [int]$WorkerMinInstances
    
    [ValidateRange(1, 30)]
    [ValidateNotNullOrEmpty()]
    [int]$WorkerMaxInstances

    [ValidateRange(1, 20)]
    [ValidateNotNullOrEmpty()]
    [int]$WorkerPlanCount

    [ValidateNotNullOrEmpty()]
    [string[]]$WorkerPlanLocations

    [ValidateRange(1, 20)]
    [ValidateNotNullOrEmpty()]
    [int]$WorkerCountPerPlan
    
    [ValidateSet("Information", "Warning")]
    [string]$WorkerLogLevel

    [ValidateNotNullOrEmpty()]
    [Hashtable]$WebsiteConfig
    
    [ValidateNotNullOrEmpty()]
    [Hashtable]$WorkerConfig

    [ValidateNotNullOrEmpty()]
    [string]$ResourceGroupName
    
    [ValidateNotNullOrEmpty()]
    [ValidatePattern("^[a-z0-9]+$")]
    [ValidateLength(1, 24)]
    [string]$StorageAccountName
    
    [ValidateNotNullOrEmpty()]
    [string]$StorageEndpointSuffix
    
    [ValidateNotNullOrEmpty()]
    [ValidateLength(1, 24)]
    [string]$KeyVaultName
    
    [ValidateNotNullOrEmpty()]
    [string]$LocalDeploymentContainerName
    
    [ValidateNotNullOrEmpty()]
    [string]$SpotWorkerDeploymentContainerName
    
    [ValidateNotNullOrEmpty()]
    [string]$LeaseContainerName
    
    [ValidateNotNullOrEmpty()]
    [TimeSpan]$RegenerationPeriod
    
    [ValidateNotNullOrEmpty()]
    [bool]$AutoRegenerateStorageKey
    
    [ValidateNotNullOrEmpty()]
    [bool]$UseSpotWorkers

    [ValidateNotNullOrEmpty()]
    [string]$RuntimeIdentifier
    
    [string]$SpotWorkerAdminUsername
    [string]$SpotWorkerAdminPassword
    [object[]]$SpotWorkerSpecs
    
    [string]$SubscriptionId
    [string]$ServiceTreeId
    [string]$EnvironmentName
    [string]$AlertEmail
    [string]$AlertPrefix
    [string]$ExistingWebsitePlanId
    [string]$WebsiteAadAppName
    [string]$WebsiteAadAppClientId

    ResourceSettings(
        [string]$ConfigName,
        [string]$StampName,
        [Hashtable]$DeploymentConfig,
        [Hashtable]$WebsiteConfig,
        [Hashtable]$WorkerConfig,
        [string]$RuntimeIdentifier) {

        # Required settings
        $this.ConfigName = $ConfigName
        $this.StampName = $StampName
        $this.WebsiteConfig = $WebsiteConfig
        $this.WorkerConfig = $WorkerConfig
        $this.RuntimeIdentifier = $RuntimeIdentifier

        $defaults = New-Object System.Collections.ArrayList
        function Set-OrDefault($key, $default, $target, $keyPrefix) {
            if ($null -eq $target) {
                $source = $DeploymentConfig
                $target = $this
            }
            else { 
                $source = $target
            }

            if ($null -eq $source[$key]) {
                $defaults.Add("$($keyPrefix)$($key)")
                $target.$key = $default
            }
            else {
                $target.$key = $source[$key]
            }
        }

        # Settings with defaults
        if ($DeploymentConfig.WebsiteAadAppClientId) {
            $this.WebsiteAadAppName = $null
            $this.WebsiteAadAppClientId = $DeploymentConfig.WebsiteAadAppClientId
        }
        else {
            Set-OrDefault "WebsiteAadAppName" "NuGet.Insights-$StampName-Website"
            $this.WebsiteAadAppClientId = $null
        }
        
        Set-OrDefault Location "West US 2"
        Set-OrDefault LogAnalyticsWorkspaceName "NuGetInsights-$StampName"
        Set-OrDefault AppInsightsName "NuGetInsights-$StampName"
        Set-OrDefault AppInsightsDailyCapGb 1
        Set-OrDefault ActionGroupName "NuGetInsights-$StampName"
        Set-OrDefault ActionGroupShortName "NI$(if ($StampName.Length -gt 10) { $StampName.Substring(0, 10) } else { $StampName } )"
        Set-OrDefault AlertEmail ""
        Set-OrDefault AlertPrefix ""
        Set-OrDefault WebsiteName "NuGetInsights-$StampName"
        Set-OrDefault WebsitePlanName "$($this.WebsiteName)-WebsitePlan"
        Set-OrDefault WorkerNamePrefix "NuGetInsights-$StampName-Worker-"
        Set-OrDefault UserManagedIdentityName "NuGetInsights-$StampName"
        Set-OrDefault WorkerPlanNamePrefix "NuGetInsights-$StampName-WorkerPlan-"
        Set-OrDefault WorkerAutoscaleNamePrefix "NuGetInsights-$StampName-WorkerAutoscale-"
        Set-OrDefault WorkerHostId "NuGetInsights-$StampName"
        Set-OrDefault WorkerSku "Y1"
        Set-OrDefault WorkerMinInstances 1
        $isPremiumPlan = $this.WorkerSku.StartsWith("P")
        $defaultWorkerMaxInstances = if ($isPremiumPlan) { 30 } else { 10 }
        Set-OrDefault WorkerMaxInstances $defaultWorkerMaxInstances
        Set-OrDefault WorkerPlanCount 1
        Set-OrDefault WorkerPlanLocations @($this.Location)
        Set-OrDefault WorkerCountPerPlan 1
        Set-OrDefault WorkerLogLevel "Warning"
        Set-OrDefault ResourceGroupName "NuGet.Insights-$StampName"
        Set-OrDefault StorageAccountName "nugin$($StampName.Replace('-', '').ToLowerInvariant())"
        Set-OrDefault StorageEndpointSuffix "core.windows.net"
        Set-OrDefault KeyVaultName "nugin$($StampName.Replace('-', '').ToLowerInvariant())"
        Set-OrDefault UseSpotWorkers $false

        # Optional settings
        $this.SubscriptionId = $DeploymentConfig.SubscriptionId
        $this.ServiceTreeId = $DeploymentConfig.ServiceTreeId
        $this.EnvironmentName = $DeploymentConfig.EnvironmentName
        $this.ExistingWebsitePlanId = $DeploymentConfig.ExistingWebsitePlanId

        # Spot worker settings
        if ($this.UseSpotWorkers) {
            Set-OrDefault SpotWorkerAdminUsername "insights"
            Set-OrDefault SpotWorkerAdminPassword ""
            Set-OrDefault SpotWorkerSpecs @(@{})

            if (!$this.SpotWorkerSpecs) {
                throw "At least one spot worker spec is required when UseSpotWorkers is true. Remove the SpotWorkerSpecs array property for default settings or add at least one object to the array."
            }
            else {
                for ($i = 0; $i -lt $this.SpotWorkerSpecs.Count; $i++) {
                    $keyPrefix = "SpotWorkerSpecs[$i]."
                    $target = $this.SpotWorkerSpecs[$i]

                    Set-OrDefault NamePrefix "NuGetInsights-$StampName-SpotWorker-$i-" $target $keyPrefix
                    Set-OrDefault Location $this.Location $target $keyPrefix
                    Set-OrDefault Sku "Standard_D2as_v4" $target $keyPrefix
                    Set-OrDefault MinInstances 1 $target $keyPrefix
                    Set-OrDefault MaxInstances 30 $target $keyPrefix
                    Set-OrDefault AddLoadBalancer $false $target $keyPrefix
                }
            }
        }

        # Static settings
        $this.LocalDeploymentContainerName = "localdeployment"
        $this.SpotWorkerDeploymentContainerName = "spotworkerdeployment"
        $this.LeaseContainerName = "leases"
        $this.RegenerationPeriod = New-TimeSpan -Days 14

        $isNuGetPackageExplorerToCsvEnabled = "NuGetPackageExplorerToCsv" -notin $this.WorkerConfig["NuGetInsights"].DisabledDrivers
        $isConsumptionPlan = $this.WorkerSku -eq "Y1"
        
        if ($isNuGetPackageExplorerToCsvEnabled) {
            if ($isConsumptionPlan) {
                # Default "MoveTempToHome" to be true when NuGetPackageExplorerToCsv is enabled. We do this because the NuGet
                # Package Explorer symbol validation APIs are hard-coded to use TEMP and can quickly fill up the small TEMP
                # capacity on consumption plan (~500 MiB). Therefore, we move TEMP to HOME at the start of the process. HOME
                # points to a Azure Storage File share which has no capacity issues.
                if ($null -eq $this.WorkerConfig["NuGetInsights"].MoveTempToHome) {
                    $this.WorkerConfig["NuGetInsights"].MoveTempToHome = $true
                }
    
                # Default the maximum number of workers per Function App plan to 16 when NuGetPackageExplorerToCsv is enabled.
                # We do this because it's easy for a lot of Function App workers to overload the HOME directory which is backed
                # by an Azure Storage File share.
                if ($null -eq $this.WorkerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT) {
                    $this.WorkerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT = 16
                }
            }

            # Reduce the storage queue trigger batch size when NuGetPackageExplorerToCsv is enabled. We do this to
            # reduce the parallelism in the worker process so that we can easily control the number of total parallel
            # queue messages are being processed and therefore are using the HOME file share.
            if ($null -eq $this.WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize) {
                if ($isPremiumPlan -or $this.UseSpotWorkers) {
                    $this.WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize = 8
                }
                else {
                    $this.WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize = 2
                }
            }
        }

        # Since Azure Functions isolated SDK does not support an INameResolver, we have to explicitly have the queue
        # names used by Azure Functions triggers.
        # Blocker: https://github.com/Azure/azure-functions-dotnet-worker/issues/393
        if ($null -eq $this.WorkerConfig["NuGetInsights"].WorkQueueName) {
            $this.WorkerConfig["NuGetInsights"].WorkQueueName = "work"
        }
        if ($null -eq $this.WorkerConfig["NuGetInsights"].ExpandQueueName) {
            $this.WorkerConfig["NuGetInsights"].ExpandQueueName = "expand"
        }
        
        if ($isConsumptionPlan) {            
            # Since Consumption plan requires WEBSITE_CONTENTAZUREFILECONNECTIONSTRING and this does not support SAS-based
            # connection strings, don't auto-regenerate in this case. We would need to regularly update a connection string based
            # on the active storage access key, which isn't worth the effort for this approach that is less secure anyway.
            $this.AutoRegenerateStorageKey = $false
        }
        else {
            $this.AutoRegenerateStorageKey = $true
        }

        # It's okay to leave this as a default since it is generated at deployment time by default.
        $defaults.Remove("SpotWorkerAdminPassword")

        if ($DeploymentConfig.RejectDefaults -and $defaults) {
            throw "Defaults are not allowed for config '$ConfigName'. Specify the following properties on the object at JSON path $.deployment: $defaults"
        }
    }
}
function Write-Status ($message) {
    Write-Host $message -ForegroundColor Green
}

# Source: https://4sysops.com/archives/convert-json-to-a-powershell-hash-table/
function ConvertTo-Hashtable {
    [CmdletBinding()]
    [OutputType('hashtable')]
    param (
        [Parameter(ValueFromPipeline)]
        $InputObject
    )
 
    process {
        ## Return null if the input is null. This can happen when calling the function
        ## recursively and a property is null
        if ($null -eq $InputObject) {
            return $null
        }
 
        ## Check if the input is an array or collection. If so, we also need to convert
        ## those types into hash tables as well. This function will convert all child
        ## objects into hash tables (if applicable)
        if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
            $collection = @(
                foreach ($object in $InputObject) {
                    ConvertTo-Hashtable -InputObject $object
                }
            )
 
            ## Return the array but don't enumerate it because the object may be pretty complex
            Write-Output -NoEnumerate $collection
        }
        elseif ($InputObject -is [psobject]) {
            ## If the object has properties that need enumeration
            ## Convert it to its own hash table and return it
            $hash = @{}
            foreach ($property in $InputObject.PSObject.Properties) {
                $hash[$property.Name] = ConvertTo-Hashtable -InputObject $property.Value
            }
            $hash
        }
        else {
            ## If the object isn't an array, collection, or other object, it's already a hash table
            ## So just return it.
            $InputObject
        }
    }
}

function Merge-Hashtable {
    $output = @{}
    foreach ($hashtable in ($Input + $Args)) {
        if ($hashtable -is [Hashtable]) {
            foreach ($key in $hashtable.Keys) {
                if (($output.ContainsKey($key)) -and ($output.$key -is [Hashtable]) -and ($hashtable.$key -is [Hashtable])) {
                    $output.$key = Merge-Hashtable $output.$key $hashtable.$key
                }
                else {
                    $output.$key = $hashtable.$key
                }
            }
        }
    }
    $output
}

function ConvertTo-FlatConfig {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)]
        $InputObject
    )

    process {
        function MakeKeys($output, $prefix, $current) {
            if ($prefix) {
                $nextPrefix = $prefix + "__"
            }
            else {
                $nextPrefix = ""
            }

            if ($current -is [HashTable]) {
                foreach ($key in $current.Keys) {
                    MakeKeys $output "$nextPrefix$key" $current.$key
                }
            }
            elseif ($current -is [System.Collections.IEnumerable] -and $current -isnot [string]) {
                $i = 0
                foreach ($value in $current) {
                    MakeKeys $output "$nextPrefix$i" $value
                    $i++
                }
            }
            else {
                $output[$prefix] = $current
            }
        }

        $output = @{}
        MakeKeys $output "" $InputObject
        $output
    }
}

function ConvertTo-NameValuePairs {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)]
        $InputObject
    )

    process {
        $output = @()
        foreach ($key in $InputObject.Keys | Sort-Object) {
            $output += [ordered]@{ name = $key; value = $InputObject.$key }
        }
        $output
    }
}

function Get-ConfigPath($ConfigName) {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../config/$ConfigName.json"))
}

function Get-ResourceSettings($ConfigName, $StampName, $RuntimeIdentifier) {
    $configPath = Get-ConfigPath $ConfigName
    Write-Status "Using config path: $configPath"
    $StampName = if (!$StampName) { $ConfigName } else { $StampName }
    Write-Status "Using stamp name: $StampName"

    function Get-Config() { Get-Content $configPath | ConvertFrom-Json | ConvertTo-Hashtable }
    function Get-AppConfig() { @{ "NuGetInsights" = @{} } }

    # Prepare the website config
    $websiteConfig = Get-Config
    $websiteConfig = Merge-Hashtable (Get-AppConfig) $websiteConfig.AppSettings.Shared $websiteConfig.AppSettings.Website

    # Prepare the worker config
    $workerConfig = Get-Config
    $workerConfig = Merge-Hashtable (Get-AppConfig) $workerConfig.AppSettings.Shared $workerConfig.AppSettings.Worker

    $deploymentConfig = Get-Config
    $deploymentConfig = $deploymentConfig.Deployment

    [ResourceSettings]::new(
        $ConfigName,
        $StampName,
        $deploymentConfig,
        $WebsiteConfig,
        $WorkerConfig,
        $RuntimeIdentifier)
}

function New-WorkerStandaloneEnv($ResourceSettings) {
    $config = $ResourceSettings.WorkerConfig | ConvertTo-FlatConfig

    # These set up the Azure Functions job host or match with the normal Azure Functions worker settings.
    $config["ASPNETCORE_SUPPRESSSTATUSMESSAGES"] = "true";
    $config["AzureFunctionsJobHost__logging__Console__IsEnabled"] = "false";
    $config["AzureFunctionsJobHost__logging__LogLevel__Default"] = $ResourceSettings.WorkerLogLevel;
    $config["AzureFunctionsWebHost__hostId"] = $ResourceSettings.WorkerHostId;
    $config["AzureWebJobs.MetricsFunction.Disabled"] = "true";
    $config["AzureWebJobs.TimerFunction.Disabled"] = "true";
    $config["AzureWebJobsFeatureFlags"] = "EnableWorkerIndexing";
    $config["AzureWebJobsStorage__accountName"] = $ResourceSettings.StorageAccountName;
    $config["AzureWebJobsStorage__credential"] = "managedidentity";
    $config["DOTNET_gcServer"] = "1";
    $config["FUNCTIONS_WORKER_RUNTIME_VERSION"] = "7.0";
    $config["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated";
    $config["logging__ApplicationInsights__LogLevel__Default"] = $ResourceSettings.WorkerLogLevel;
    $config["logging__LogLevel__Default"] = $ResourceSettings.WorkerLogLevel;
    $config["NUGET_INSIGHTS_ALLOW_ICU"] = "true";
    $config["NuGetInsights__LeaseContainerName"] = $ResourceSettings.LeaseContainerName;
    $config["NuGetInsights__StorageAccountName"] = $ResourceSettings.StorageAccountName;
    $config["QueueTriggerConnection__credential"] = "managedidentity";
    $config["QueueTriggerConnection__queueServiceUri"] = "https://$($ResourceSettings.StorageAccountName).queue.$($ResourceSettings.StorageEndpointSuffix)/";
    $config["WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED"] = "1";

    # These are overridden by Install-WorkerStandalone.ps1. 
    $config["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "PLACEHOLDER";
    $config["ASPNETCORE_URLS"] = "PLACEHOLDER";
    $config["AzureWebJobsScriptRoot"] = "PLACEHOLDER";
    $config["AzureWebJobsStorage__clientId"] = "PLACEHOLDER";
    $config["NuGetInsights__DeploymentLabel"] = "PLACEHOLDER";
    $config["NuGetInsights__UserManagedIdentityClientId"] = "PLACEHOLDER";
    $config["QueueTriggerConnection__clientId"] = "PLACEHOLDER";
    $config["WEBSITE_HOSTNAME"] = "PLACEHOLDER";
    $config["WEBSITE_SITE_NAME"] = "PLACEHOLDER";

    return $config
}

function Out-EnvFile() {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)]
        $InputObject,

        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    process {
        ($InputObject.GetEnumerator() | `
            ForEach-Object { "$($_.Key)=$($_.Value)" } | `
            Sort-Object) `
            -Join [Environment]::NewLine | `
            Out-File -FilePath $FilePath
    }
}

function Get-RandomPassword() {
    $random = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    $buffer = New-Object byte[](32)
    $random.GetBytes($buffer)
    $password = "N1!" + [Convert]::ToBase64String($buffer)
    return $password
}

function New-MainParameters(
    $ResourceSettings,
    $DeploymentLabel,
    $WebsiteZipUrl,
    $WorkerZipUrl,
    $SpotWorkerUploadScriptUrl,
    $AzureFunctionsHostZipUrl,
    $WorkerStandaloneEnvUrl,
    $InstallWorkerStandaloneUrl) {

    if ($ResourceSettings.RuntimeIdentifier -eq "linux-x64") {
        $isDeploymentLinux = $true
    }
    elseif ($ResourceSettings.RuntimeIdentifier -eq "win-x64") {
        $isDeploymentLinux = $false
    }
    else {
        throw "Unexpected runtime identifier '$($ResourceSettings.RuntimeIdentifier)' for deployment. Only 'win-x64' and 'linux-64' are supported. macOS app services are not supported by Azure."
    }

    $parameters = @{
        actionGroupName           = $ResourceSettings.ActionGroupName;
        actionGroupShortName      = $ResourceSettings.ActionGroupShortName;
        alertEmail                = $ResourceSettings.AlertEmail;
        alertPrefix               = $ResourceSettings.AlertPrefix;
        logAnalyticsWorkspaceName = $ResourceSettings.LogAnalyticsWorkspaceName;
        appInsightsDailyCapGb     = $ResourceSettings.AppInsightsDailyCapGb;
        appInsightsName           = $ResourceSettings.AppInsightsName;
        deploymentLabel           = $DeploymentLabel;
        keyVaultName              = $ResourceSettings.KeyVaultName;
        leaseContainerName        = $ResourceSettings.LeaseContainerName;
        location                  = $ResourceSettings.Location;
        storageAccountName        = $ResourceSettings.StorageAccountName;
        useSpotWorkers            = $ResourceSettings.UseSpotWorkers;
        userManagedIdentityName   = $ResourceSettings.UserManagedIdentityName;
        websiteAadClientId        = $ResourceSettings.WebsiteAadAppClientId;
        websiteConfig             = @($ResourceSettings.WebsiteConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        websiteIsLinux            = $isDeploymentLinux;
        websiteName               = $ResourceSettings.WebsiteName;
        websiteZipUrl             = $websiteZipUrl;
        workerAutoscaleNamePrefix = $ResourceSettings.WorkerAutoscaleNamePrefix;
        workerConfig              = @($ResourceSettings.WorkerConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        workerCountPerPlan        = $ResourceSettings.WorkerCountPerPlan;
        workerHostId              = $ResourceSettings.WorkerHostId;
        workerLogLevel            = $ResourceSettings.WorkerLogLevel;
        workerMaxInstances        = $ResourceSettings.WorkerMaxInstances;
        workerMinInstances        = $ResourceSettings.WorkerMinInstances;
        workerNamePrefix          = $ResourceSettings.WorkerNamePrefix;
        workerPlanCount           = $ResourceSettings.WorkerPlanCount;
        workerPlanLocations       = $ResourceSettings.WorkerPlanLocations;
        workerPlanNamePrefix      = $ResourceSettings.WorkerPlanNamePrefix;
        workerSku                 = $ResourceSettings.WorkerSku;
        workerIsLinux             = $isDeploymentLinux;
        workerZipUrl              = $workerZipUrl
    }

    if ($ResourceSettings.ExistingWebsitePlanId) {
        $parameters.websitePlanId = $ResourceSettings.ExistingWebsitePlanId
    }
    else {
        $parameters.websitePlanName = $ResourceSettings.WebsitePlanName
    }

    if ($ResourceSettings.UseSpotWorkers) {
        $parameters.spotWorkerUploadScriptUrl = $SpotWorkerUploadScriptUrl;
        $parameters.spotWorkerDeploymentUrls = @{ files = @(
                $WorkerZipUrl,
                $AzureFunctionsHostZipUrl,
                $WorkerStandaloneEnvUrl,
                $InstallWorkerStandaloneUrl
            )
        };
        $parameters.spotWorkerDeploymentContainerName = $ResourceSettings.SpotWorkerDeploymentContainerName
        $parameters.spotWorkerAdminUsername = $ResourceSettings.SpotWorkerAdminUsername;
        $parameters.spotWorkerAdminPassword = $ResourceSettings.SpotWorkerAdminPassword;
        $parameters.spotWorkerSpecs = $ResourceSettings.SpotWorkerSpecs;
    }

    $parameters
}

function New-ParameterFile($Parameters, $PathReferences, $FilePath) {
    # Docs: https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/parameter-files
    $deploymentParameters = [ordered]@{
        "`$schema"     = "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#";
        contentVersion = "1.0.0.0";
        parameters     = [ordered]@{}
    }
    
    foreach ($key in $Parameters.Keys | Sort-Object) {
        $deploymentParameters.parameters.$key = @{ value = $parameters.$key }
    }

    # Docs: https://ev2docs.azure.net/getting-started/authoring/parameters/parameters.html
    foreach ($pathReference in $PathReferences) {
        if (!$deploymentParameters.paths) {
            $deploymentParameters.paths = @()
        }

        $deploymentParameters.paths += @{ parameterReference = $pathReference }
    }

    $dirPath = Split-Path $FilePath
    if (!(Test-Path $dirPath)) {
        New-Item $dirPath -ItemType Directory | Out-Null
    }

    $deploymentParameters | ConvertTo-Json -Depth 100 | Out-File $FilePath -Encoding UTF8
}

function Get-Bicep([switch]$DoNotThrow) {
    if (Get-Command bicep -CommandType Application -ErrorAction Ignore) {
        $bicepExe = "bicep"
        $bicepArgs = @("build")
    }
    elseif (Get-Command az -CommandType Application -ErrorAction Ignore) {
        $bicepExe = "az"
        $bicepArgs = @("bicep", "build", "--file")
    }
    elseif (!$DoNotThrow) {
        throw "Neither 'bicep' or 'az' (for 'az bicep') commands could be found. Installation instructions: https://docs.microsoft.com/azure/azure-resource-manager/bicep/install"
    }

    return $bicepExe, $bicepArgs
}

function New-Deployment($ResourceGroupName, $DeploymentDir, $DeploymentLabel, $DeploymentName, $BicepPath, $Parameters) {
    $parametersPath = Join-Path $DeploymentDir "$DeploymentName.deploymentParameters.json"
    New-ParameterFile $Parameters @() $parametersPath

    $bicepPath = Join-Path $PSScriptRoot $BicepPath
    $templatePath = Join-Path $DeploymentDir "$DeploymentName.deploymentTemplate.json"
    $bicepExe, $bicepArgs = Get-Bicep
    & $bicepExe @bicepArgs $bicepPath --outfile $templatePath
    if ($LASTEXITCODE -ne 0) {
        throw "Command 'bicep build' failed with exit code $LASTEXITCODE."
    }

    return New-AzResourceGroupDeployment `
        -TemplateFile $templatePath `
        -ResourceGroupName $ResourceGroupName `
        -Name "$DeploymentLabel-$DeploymentName" `
        -TemplateParameterFile $parametersPath `
        -ErrorAction Stop
}

function Get-AppServiceBaseUrl($name) {
    "https://$($name.ToLowerInvariant()).azurewebsites.net"
}

function Approve-SubscriptionId($configuredSubscriptionId) {
    # Confirm the target subscription.
    $context = Get-AzContext -ErrorAction Stop
    $example = if ($configuredSubscriptionId) { $configuredSubscriptionId } else { "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" }
    $example = "Use 'Set-AzContext -Subscription $example' to continue."
    if (!$context.Subscription.Id) {
        throw "No active subscription was found. $example"
    }
    elseif (!$configuredSubscriptionId) {
        $title = "You are about to deploy to subscription $($context.Subscription.Id)."
        $question = 'Are you sure you want to proceed?'
        $choices = '&Yes', '&No'
        $decision = $Host.UI.PromptForChoice($title, $question, $choices, 1)
        if ($decision -ne 0) {
            exit
        }
    }
    elseif ($configuredSubscriptionId -and $configuredSubscriptionId -ne $context.Subscription.Id) {
        throw "The current active subscription ($($context.Subscription.Id)) does not match configuration. $example"
    }
    Write-Status "Using subscription: $($context.Subscription.Id)"
}

function Get-ConfigNameDynamicParameter($type, $name) {
    $parameterAttribute = [System.Management.Automation.ParameterAttribute]@{
        Mandatory = $true
    }

    $configNames = Get-ChildItem -Path (Join-Path $PSScriptRoot "../config/*.json") | Select-Object -ExpandProperty BaseName
    $validateSetAttribute = New-Object System.Management.Automation.ValidateSetAttribute($configNames)

    $attributeCollection = [System.Collections.ObjectModel.Collection[System.Attribute]]::new()
    $attributeCollection.Add($parameterAttribute)
    $attributeCollection.Add($validateSetAttribute)
    
    
    $parameter = [System.Management.Automation.RuntimeDefinedParameter]::new(
        $name, $type, $attributeCollection
    )

    return $parameter
}

function Get-DeploymentLocals($DeploymentLabel, $DeploymentDir) {
    if (!$DeploymentLabel) {
        $DeploymentLabel = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")
        Write-Status "Using deployment label: $DeploymentLabel"
    }

    if (!$DeploymentDir) {
        $DeploymentDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../artifacts/deploy"))
        Write-Status "Using deployment directory: $DeploymentDir"
    }

    return $DeploymentLabel, $DeploymentDir
}

function Get-DefaultRuntimeIdentifier($RuntimeIdentifier, $WriteDefault = $true) {
    if (!$RuntimeIdentifier) {
        if ($IsLinux -or $IsMacOs) {
            $RuntimeIdentifier = "linux-x64"
        }
        else {
            $RuntimeIdentifier = "win-x64"
        }

        if ($WriteDefault) {
            Write-Host "The -RuntimeIdentifier parameter has been given a default value of '$RuntimeIdentifier'." -ForegroundColor Green
        }
    }

    return $RuntimeIdentifier
}

# Source: https://stackoverflow.com/a/57599481
if (Get-TypeData -TypeName System.Array) {
    Remove-TypeData System.Array
}