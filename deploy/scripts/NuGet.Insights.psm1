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
    [string]$WebsiteLocation
    
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
    [string]$WorkQueueName
    
    [ValidateNotNullOrEmpty()]
    [string]$ExpandQueueName
    
    [ValidateNotNullOrEmpty()]
    [string]$DeploymentNamePrefix
    
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
        Set-OrDefault WebsiteLocation $this.Location
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
        Set-OrDefault LeaseContainerName "leases"
        Set-OrDefault WorkQueueName "work"
        Set-OrDefault ExpandQueueName "expand"
        Set-OrDefault StorageEndpointSuffix "core.windows.net"
        Set-OrDefault KeyVaultName "nugin$($StampName.Replace('-', '').ToLowerInvariant())"
        Set-OrDefault DeploymentNamePrefix "NuGetInsights-$StampName-Deployment-"
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
            if ($null -eq $this.WorkerConfig.AzureFunctionsJobHost.extensions.queues.batchSize) {
                if ($isPremiumPlan -or $this.UseSpotWorkers) {
                    $batchSize = 12
                }
                else {
                    $batchSize = 2
                }

                $this.WorkerConfig = Merge-Hashtable $this.WorkerConfig @{
                    AzureFunctionsJobHost = @{
                        extensions = @{
                            queues = @{
                                batchSize = $batchSize
                            }
                        }
                    }
                }
            }
        }

        function Add-SharedAppSettings($settings) {
            Merge-Hashtable $settings (@{
                    # Promote config used during the deploment to the matching app config.
                    NuGetInsights = @{
                        StorageAccountName = $this.StorageAccountName;
                        LeaseContainerName = $this.LeaseContainerName;
                    
                        # Since Azure Functions isolated SDK does not support an INameResolver, we have to explicitly have the queue
                        # names used by Azure Functions triggers.
                        # Blocker: https://github.com/Azure/azure-functions-dotnet-worker/issues/393
                        WorkQueueName      = $this.WorkQueueName;
                        ExpandQueueName    = $this.ExpandQueueName;
                    };
                })
        }

        $this.WebsiteConfig = Add-SharedAppSettings $this.WebsiteConfig
        $this.WebsiteConfig = Merge-Hashtable $this.WebsiteConfig (@{
                AzureAd = @{
                    Instance = "https://login.microsoftonline.com/";
                    ClientId = $this.WebsiteAadAppClientId;
                    TenantId = "common";
                }
            })

        $this.WorkerConfig = Add-SharedAppSettings $this.WorkerConfig
        $this.WorkerConfig = Merge-Hashtable $this.WorkerConfig (@{
                FUNCTIONS_WORKER_RUNTIME_VERSION = "8.0";
                FUNCTIONS_WORKER_RUNTIME         = "dotnet-isolated";
                AzureFunctionsWebHost            = @{
                    hostId = $this.WorkerHostId;
                };
                AzureWebJobsStorage              = @{
                    accountName = $this.StorageAccountName;
                };
                QueueTriggerConnection           = @{
                    accountName = $this.StorageAccountName;
                };
                AzureFunctionsJobHost            = @{
                    logging = @{
                        LogLevel = @{
                            Default = $this.WorkerLogLevel;
                        };
                    };
                };
                logging                          = @{
                    ApplicationInsights = @{
                        LogLevel = @{
                            Default = $this.WorkerLogLevel;
                        };
                    };
                    LogLevel            = @{
                        Default = $this.WorkerLogLevel;
                    };
                };
            })

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
                $output[$prefix] = "$current"
            }
        }

        $output = @{}
        MakeKeys $output "" $InputObject
        $output
    }
}

function Get-ConfigPath($ConfigName) {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../config/$ConfigName.json"))
}

function Get-ResourceSettings($ConfigName, $StampName, $RuntimeIdentifier, $ConfigOverride) {
    $configPath = Get-ConfigPath $ConfigName
    Write-Status "Using config path: $configPath"
    $StampName = if (!$StampName) { $ConfigName } else { $StampName }
    Write-Status "Using stamp name: $StampName"

    function Get-Config() {
        $output = Get-Content $configPath | ConvertFrom-Json | ConvertTo-Hashtable
        if ($ConfigOverride) {
            $output = Merge-Hashtable $output $ConfigOverride
        }
        return $output
    }
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
    $config = Merge-Hashtable $ResourceSettings.WorkerConfig (@{
            APPLICATIONINSIGHTS_CONNECTION_STRING   = "PLACEHOLDER";
            ASPNETCORE_URLS                         = "PLACEHOLDER";
            ASPNETCORE_SUPPRESSSTATUSMESSAGES       = $true;
            AzureFunctionsJobHost                   = @{
                logging = @{
                    Console = @{
                        IsEnabled = $false;
                    };
                };
            };
            "AzureWebJobs.MetricsFunction.Disabled" = $true;
            "AzureWebJobs.TimerFunction.Disabled"   = $true;
            AzureWebJobsFeatureFlags                = "EnableWorkerIndexing";
            AzureWebJobsScriptRoot                  = "PLACEHOLDER"
            AzureWebJobsStorage                     = @{ 
                credential = "managedidentity";
                clientId   = "PLACEHOLDER";
            };
            DOTNET_gcServer                         = "1";
            NUGET_INSIGHTS_ALLOW_ICU                = $true;
            NuGetInsights                           = @{
                DeploymentLabel             = "PLACEHOLDER";
                UserManagedIdentityClientId = "PLACEHOLDER";
            };
            QueueTriggerConnection                  = @{
                credential = "managedidentity";
                clientId   = "PLACEHOLDER";
            };
            WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED  = "1";
            WEBSITE_HOSTNAME                        = "PLACEHOLDER";
            WEBSITE_SITE_NAME                       = "PLACEHOLDER";
        })

    return $config | ConvertTo-FlatConfig
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

    # The website AAD client ID is set dynamically sometimes, based on app name. Ensure it makes it into the config.
    if (!$ResourceSettings.WebsiteAadAppClientId) {
        throw "A website AAD client ID is required for generating deployment parameters."
    }
    else {
        $ResourceSettings.WebsiteConfig = Merge-Hashtable $ResourceSettings.WebsiteConfig (@{
                AzureAd = @{
                    ClientId = $ResourceSettings.WebsiteAadAppClientId;
                }
            })
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
        deploymentNamePrefix      = $ResourceSettings.DeploymentNamePrefix;
        useSpotWorkers            = $ResourceSettings.UseSpotWorkers;
        userManagedIdentityName   = $ResourceSettings.UserManagedIdentityName;
        websiteConfig             = $ResourceSettings.WebsiteConfig | ConvertTo-FlatConfig | Get-OrderedHashtable;
        websiteIsLinux            = $isDeploymentLinux;
        websiteName               = $ResourceSettings.WebsiteName;
        websiteZipUrl             = $websiteZipUrl;
        websiteLocation           = $ResourceSettings.WebsiteLocation;
        workerAutoscaleNamePrefix = $ResourceSettings.WorkerAutoscaleNamePrefix;
        workerConfig              = $ResourceSettings.WorkerConfig | ConvertTo-FlatConfig | Get-OrderedHashtable;
        workerCountPerPlan        = $ResourceSettings.WorkerCountPerPlan;
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
        $parameters.spotWorkerHostZipUrl = $AzureFunctionsHostZipUrl;
        $parameters.spotWorkerEnvUrl = $WorkerStandaloneEnvUrl;
        $parameters.spotWorkerInstallScriptUrl = $InstallWorkerStandaloneUrl;
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

    $deploymentParameters | ConvertTo-Json -Depth 100 | Format-Json | Out-File $FilePath -Encoding UTF8
}

function Get-OrderedHashtable {
    $output = [ordered]@{}
    foreach ($hashtable in ($Input + $Args)) {
        if ($hashtable -is [Hashtable]) {
            foreach ($key in $hashtable.Keys | Sort-Object) {
                if ($hashtable.$key -is [Hashtable]) {
                    $output.$key = Get-OrderedHashtable $hashtable.$key
                }
                else {
                    $output.$key = $hashtable.$key
                }
            }
        }
    }
    return $output
}

# Source: https://stackoverflow.com/a/71664664
function Format-Json {
    <#
    .SYNOPSIS
        Prettifies JSON output.
        Version January 3rd 2024
        Fixes:
            - empty [] or {} or in-line arrays as per https://stackoverflow.com/a/71664664/9898643
              by Widlov (https://stackoverflow.com/users/1716283/widlov)
            - Unicode Apostrophs \u0027 as written by ConvertTo-Json are replaced with regular single quotes "'"
            - multiline empty [] or {} are converted into inline arrays or objects
    .DESCRIPTION
        Reformats a JSON string so the output looks better than what ConvertTo-Json outputs.
    .PARAMETER Json
        Required: [string] The JSON text to prettify.
    .PARAMETER Minify
        Optional: Returns the json string compressed.
    .PARAMETER Indentation
        Optional: The number of spaces (1..1024) to use for indentation. Defaults to 2.
    .PARAMETER AsArray
        Optional: If set, the output will be in the form of a string array, otherwise a single string is output.
    .EXAMPLE
        $json | ConvertTo-Json | Format-Json -Indentation 4
    .OUTPUTS
        System.String or System.String[] (the latter when parameter AsArray is set)
    #>
    [CmdletBinding(DefaultParameterSetName = 'Prettify')]
    Param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true)]
        [string]$Json,

        [Parameter(ParameterSetName = 'Minify')]
        [switch]$Minify,

        [Parameter(ParameterSetName = 'Prettify')]
        [ValidateRange(1, 1024)]
        [int]$Indentation = 2,

        [Parameter(ParameterSetName = 'Prettify')]
        [switch]$AsArray
    )

    if ($PSCmdlet.ParameterSetName -eq 'Minify') {
        return ($Json | ConvertFrom-Json) | ConvertTo-Json -Depth 100 -Compress
    }

    # If the input JSON text has been created with ConvertTo-Json -Compress
    # then we first need to reconvert it without compression
    if ($Json -notmatch '\r?\n') {
        $Json = ($Json | ConvertFrom-Json) | ConvertTo-Json -Depth 100
    }

    $indent = 0
    $regexUnlessQuoted = '(?=([^"]*"[^"]*")*[^"]*$)'

    $result = ($Json -split '\r?\n' | ForEach-Object {
            # If the line contains a ] or } character, 
            # we need to decrement the indentation level unless:
            #   - it is inside quotes, AND
            #   - it does not contain a [ or {
            if (($_ -match "[}\]]$regexUnlessQuoted") -and ($_ -notmatch "[\{\[]$regexUnlessQuoted")) {
                $indent = [Math]::Max($indent - $Indentation, 0)
            }

            # Replace all colon-space combinations by ": " unless it is inside quotes.
            $line = (' ' * $indent) + ($_.TrimStart() -replace ":\s+$regexUnlessQuoted", ': ')

            # If the line contains a [ or { character, 
            # we need to increment the indentation level unless:
            #   - it is inside quotes, AND
            #   - it does not contain a ] or }
            if (($_ -match "[\{\[]$regexUnlessQuoted") -and ($_ -notmatch "[}\]]$regexUnlessQuoted")) {
                $indent += $Indentation
            }

            # ConvertTo-Json returns all single-quote characters as Unicode Apostrophs \u0027
            # see: https://stackoverflow.com/a/29312389/9898643
            $line -replace '\\u0027', "'"

            # join the array with newlines and convert multiline empty [] or {} into inline arrays or objects
        }) -join [Environment]::NewLine -replace '(\[)\s+(\])', '$1$2' -replace '(\{)\s+(\})', '$1$2'

    if ($AsArray) { return , [string[]]($result -split '\r?\n') }
    $result
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

function Get-AzCurrentUser() {
    Write-Status "Determining the current user for Az PowerShell operations..."
    $graphToken = Get-AzAccessToken -Resource "https://graph.microsoft.com/"
    $graphHeaders = @{ Authorization = "Bearer $($graphToken.Token)" }
    $currentUser = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/me" -Headers $graphHeaders
    return $currentUser
}

function Invoke-WithRetryOnForbidden([scriptblock]$action, $requiredSuccesses = 0) {
    $maxRetries = 30
    $attempt = 0
    $successes = 0
    $output = $null
    while ($true) {
        try {
            $attempt++
            $output = & $action
            $successes++
            if ($successes -le $requiredSuccesses) {
                Write-Warning "Attempt $($attempt) - Succeeded, but checking again in 5 seconds to allow for propagation."
                Start-Sleep 5
                continue
            }
            break
        }
        catch {
            $successes = 0
            if ($attempt -lt $maxRetries -and (
                    $_.Exception.Status -eq 403 -or
                    $_.Exception.Response.StatusCode -eq 403 -or
                    $_.Exception.InnerException.Response.StatusCode -eq 403 -or 
                    $_.Exception.RequestInformation.HttpStatusCode -eq 403)) {
                Write-Warning "Attempt $($attempt) - HTTP 403 Forbidden. Trying again in 10 seconds."
                Start-Sleep 10
                continue
            }
            else {
                Write-Warning "No access. Have you checked the network firewall settings on the resource?"
                throw
            }
        }
    }

    return $output
}

function Add-AzRoleAssignmentWithRetry($currentUser, [string]$resourceGroupName, [string]$roleDefinitionName, [scriptblock]$testAccess, $requiredSuccesses = 0) {
    Write-Status "Adding $roleDefinitionName role assignment for '$($currentUser.userPrincipalName)' (object ID $($currentUser.id))..."

    $existingRoleAssignment = Get-AzRoleAssignment `
        -ResourceGroupName $resourceGroupName `
        -RoleDefinitionName $roleDefinitionName `
    | Where-Object { $_.ObjectId -eq $currentUser.id }

    if (!$existingRoleAssignment) {
        New-AzRoleAssignment `
            -ObjectId $currentUser.id `
            -ResourceGroupName $resourceGroupName `
            -RoleDefinitionName $roleDefinitionName | Out-Null
    }

    Invoke-WithRetryOnForbidden $testAccess $requiredSuccesses
}

function Remove-AzRoleAssignmentWithRetry($currentUser, [string]$resourceGroupName, [string]$roleDefinitionName, [switch]$AllowMissing) {
    Write-Status "Removing $roleDefinitionName for '$($currentUser.userPrincipalName)' (object ID $($currentUser.id))..."

    $maxRetries = 30
    $attempt = 0
    while ($true) {
        try {
            $attempt++
            Remove-AzRoleAssignment `
                -ObjectId $currentUser.id `
                -ResourceGroupName $resourceGroupName `
                -RoleDefinitionName $roleDefinitionName `
                -ErrorAction Stop
            break
        }
        catch {
            if ($attempt -lt $maxRetries -and $_.Exception.Response.StatusCode -eq 204) {
                Write-Warning "Attempt $($attempt) - HTTP 204 No Content. Trying again in 10 seconds."
                Start-Sleep 10
                continue
            }
            elseif ($attempt -lt $maxRetries -and $_.Exception.Message -eq "The provided information does not map to a role assignment.") {
                if ($allowMissing) {
                    break
                }
                Write-Warning "Attempt $($attempt) - transient duplicate role assignments. Trying again in 10 seconds."
                Start-Sleep 10
                continue
            } 
            throw
        }
    }

}

function Set-StorageFirewallDefaultAction($ResourceSettings, $DefaultAction) {
    $storageFirewall = Get-AzStorageAccountNetworkRuleSet `
        -ResourceGroupName $ResourceSettings.ResourceGroupName `
        -Name $ResourceSettings.StorageAccountName

    if ($storageFirewall.DefaultAction -ne $DefaultAction) {
        Update-AzStorageAccountNetworkRuleSet `
            -ResourceGroupName $ResourceSettings.ResourceGroupName `
            -Name $ResourceSettings.StorageAccountName `
            -DefaultAction $DefaultAction | Out-Null
    }
}

# Source: https://stackoverflow.com/a/57599481
if (Get-TypeData -TypeName System.Array) {
    Remove-TypeData System.Array
}