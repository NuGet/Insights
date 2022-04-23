class ResourceSettings {
    [ValidateNotNullOrEmpty()]
    [string]$ConfigName

    [ValidateNotNullOrEmpty()]
    [string]$StampName

    [ValidateNotNullOrEmpty()]
    [string]$Location
    
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
    [string]$WorkerHostId
    
    [ValidateNotNullOrEmpty()]
    [string]$WorkerNamePrefix
    
    [ValidateNotNullOrEmpty()]
    [string]$WorkerUserManagedIdentityName
    
    [ValidateSet("Y1", "S1", "P1v2")]
    [string]$WorkerSku
    
    [ValidateRange(1, 10)]
    [ValidateNotNullOrEmpty()]
    [int]$WorkerMinInstances

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
    [string]$DeploymentContainerName
    
    [ValidateNotNullOrEmpty()]
    [string]$LeaseContainerName
    
    [ValidateNotNullOrEmpty()]
    [TimeSpan]$RegenerationPeriod
    
    [ValidateNotNullOrEmpty()]
    [bool]$AutoRegenerateStorageKey
    
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
        [Hashtable]$WorkerConfig) {

        $d = $DeploymentConfig
        
        # Required settings
        $this.ConfigName = $ConfigName
        $this.StampName = $StampName
        $this.WebsiteConfig = $WebsiteConfig
        $this.WorkerConfig = $WorkerConfig

        $defaults = New-Object System.Collections.ArrayList
        function Set-OrDefault($key, $default) {
            if (!$d[$key]) {
                $defaults.Add($key)
                $this.$key = $default
            }
            else {
                $this.$key = $d[$key]
            }
        }

        # Settings with defaults
        if ($d.WebsiteAadAppClientId) {
            $this.WebsiteAadAppName = $null
            $this.WebsiteAadAppClientId = $d.WebsiteAadAppClientId
        }
        else {
            Set-OrDefault "WebsiteAadAppName" "NuGet.Insights-$StampName-Website"
            $this.WebsiteAadAppClientId = $null
        }
        
        Set-OrDefault Location "West US 2"
        Set-OrDefault AppInsightsName "NuGetInsights-$StampName"
        Set-OrDefault AppInsightsDailyCapGb 1
        Set-OrDefault ActionGroupName "NuGetInsights-$StampName"
        Set-OrDefault ActionGroupShortName "NI$(if ($StampName.Length -gt 10) { $StampName.Substring(0, 10) } else { $StampName } )"
        Set-OrDefault AlertEmail ""
        Set-OrDefault AlertPrefix ""
        Set-OrDefault WebsiteName "NuGetInsights-$StampName"
        Set-OrDefault WebsitePlanName "$($this.WebsiteName)-WebsitePlan"
        Set-OrDefault WorkerNamePrefix "NuGetInsights-$StampName-Worker-"
        Set-OrDefault WorkerUserManagedIdentityName "NuGetInsights-$StampName-Worker"
        Set-OrDefault WorkerPlanNamePrefix "NuGetInsights-$StampName-WorkerPlan-"
        Set-OrDefault WorkerHostId "NuGetInsights-$StampName"
        Set-OrDefault WorkerSku "Y1"
        Set-OrDefault WorkerMinInstances 1
        Set-OrDefault WorkerPlanCount 1
        Set-OrDefault WorkerPlanLocations @($this.Location)
        Set-OrDefault WorkerCountPerPlan 1
        Set-OrDefault WorkerLogLevel "Warning"
        Set-OrDefault ResourceGroupName "NuGet.Insights-$StampName"
        Set-OrDefault StorageAccountName "nugin$($StampName.ToLowerInvariant())"
        Set-OrDefault StorageEndpointSuffix "core.windows.net"
        Set-OrDefault KeyVaultName "nugin$($StampName.ToLowerInvariant())"

        # Optional settings
        $this.SubscriptionId = $d.SubscriptionId
        $this.ServiceTreeId = $d.ServiceTreeId
        $this.EnvironmentName = $d.EnvironmentName
        $this.ExistingWebsitePlanId = $d.ExistingWebsitePlanId

        # Static settings
        $this.DeploymentContainerName = "deployment"
        $this.LeaseContainerName = "leases"
        $this.RegenerationPeriod = New-TimeSpan -Days 14

        $isNuGetPackageExplorerToCsvEnabled = "NuGetPackageExplorerToCsv" -notin $this.WorkerConfig["NuGet.Insights"].DisabledDrivers
        $isConsumptionPlan = $this.WorkerSku -eq "Y1"
        
        if ($isNuGetPackageExplorerToCsvEnabled) {
            if ($isConsumptionPlan) {
                # Default "MoveTempToHome" to be true when NuGetPackageExplorerToCsv is enabled. We do this because the NuGet
                # Package Explorer symbol validation APIs are hard-coded to use TEMP and can quickly fill up the small TEMP
                # capacity on consumption plan (~500 MiB). Therefore, we move TEMP to HOME at the start of the process. HOME
                # points to a Azure Storage File share which has no capacity issues.
                if ($null -eq $this.WorkerConfig["NuGet.Insights"].MoveTempToHome) {
                    $this.WorkerConfig["NuGet.Insights"].MoveTempToHome = $true
                }
    
                # Default the maximum number of workers per Function App plan to 16 when NuGetPackageExplorerToCsv is enabled.
                # We do this because it's easy for a lot of Function App workers to overload the HOME directory which is backed
                # by an Azure Storage File share.
                if ($null -eq $this.WorkerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT) {
                    $this.WorkerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT = 16
                }
            }

            # Default the storage queue trigger batch size to 1 when NuGetPackageExplorerToCsv is enabled. We do this to
            # eliminate the parallelism in the worker process so that we can easily control the number of total parallel
            # queue messages are being processed and therefore are using the HOME file share.
            if ($null -eq $this.WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize) {
                if ($this.WorkerSku -eq "P1v2") {
                    $this.WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize = 8
                }
                else {
                    $this.WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize = 2
                }
            }
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

        if ($d.RejectDefaults -and $defaults) {
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
                $nextPrefix = $prefix + ":"
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
    Join-Path $PSScriptRoot "../config/$ConfigName.json"
}

function Get-ResourceSettings($ConfigName, $StampName) {
    $configPath = Get-ConfigPath $ConfigName
    Write-Status "Using config path: $configPath"
    $StampName = if (!$StampName) { $ConfigName } else { $StampName }
    Write-Status "Using stamp name: $StampName"

    function Get-Config() { Get-Content $configPath | ConvertFrom-Json | ConvertTo-Hashtable }
    function Get-AppConfig() { @{ "NuGet.Insights" = @{} } }

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
        $WorkerConfig)
}

function New-WorkerStandaloneEnv($ResourceSettings) {
    $config = $ResourceSettings.WebsiteConfig | ConvertTo-FlatConfig

    # Placeholder values will be overridden by the ARM deployment or by the installation script. 
    $config["APPINSIGHTS_INSTRUMENTATIONKEY"] = "PLACEHOLDER";
    $config["ASPNETCORE_URLS"] = "PLACEHOLDER";
    $config["AzureFunctionsJobHost:Logging:Console:IsEnabled"] = "false";
    $config["AzureFunctionsWebHost:hostId"] = $ResourceSettings.WorkerHostId;
    $config["AzureWebJobsScriptRoot"] = "false";
    $config["AzureWebJobsStorage:accountName"] = $ResourceSettings.StorageAccountName;
    $config["AzureWebJobsStorage:clientId"] = "PLACEHOLDER";
    $config["AzureWebJobsStorage:credential"] = "managedidentity";
    $config["NuGet.Insights:LeaseContainerName"] = $ResourceSettings.LeaseContainerName;
    $config["NuGet.Insights:StorageAccountName"] = $ResourceSettings.StorageAccountName;
    $config["NuGet.Insights:UserManagedIdentityClientId"] = "PLACEHOLDER";
    $config["QueueTriggerConnection:clientId"] = "PLACEHOLDER";
    $config["QueueTriggerConnection:credential"] = "managedidentity";
    $config["QueueTriggerConnection:queueServiceUri"] = "https://$($ResourceSettings.StorageAccountName).queue.$($ResourceSettings.StorageEndpointSuffix)/";
    $config["WEBSITE_HOSTNAME"] = "PLACEHOLDER";

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
            ForEach-Object { "$($_.Key)=$($_.Value)" }) `
            -Join [Environment]::NewLine | `
            Out-File -FilePath $FilePath
    }
}

function New-MainParameters($ResourceSettings, $WebsiteZipUrl, $WorkerZipUrl, $DeploymentLabel) {
    $parameters = @{
        deploymentLabel               = $DeploymentLabel;
        appInsightsName               = $ResourceSettings.AppInsightsName;
        appInsightsDailyCapGb         = $ResourceSettings.AppInsightsDailyCapGb;
        actionGroupName               = $ResourceSettings.ActionGroupName;
        actionGroupShortName          = $ResourceSettings.ActionGroupShortName;
        alertEmail                    = $ResourceSettings.AlertEmail;
        alertPrefix                   = $ResourceSettings.AlertPrefix;
        storageAccountName            = $ResourceSettings.StorageAccountName;
        keyVaultName                  = $ResourceSettings.KeyVaultName;
        deploymentContainerName       = $ResourceSettings.DeploymentContainerName;
        leaseContainerName            = $ResourceSettings.LeaseContainerName;
        websiteName                   = $ResourceSettings.WebsiteName;
        websiteAadClientId            = $ResourceSettings.WebsiteAadAppClientId;
        websiteConfig                 = @($ResourceSettings.WebsiteConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        websiteZipUrl                 = $websiteZipUrl;
        workerPlanNamePrefix          = $ResourceSettings.WorkerPlanNamePrefix;
        workerUserManagedIdentityName = $ResourceSettings.WorkerUserManagedIdentityName;
        workerNamePrefix              = $ResourceSettings.WorkerNamePrefix;
        workerHostId                  = $ResourceSettings.WorkerHostId;
        workerPlanCount               = $ResourceSettings.WorkerPlanCount;
        workerPlanLocations           = $ResourceSettings.WorkerPlanLocations;
        workerCountPerPlan            = $ResourceSettings.WorkerCountPerPlan;
        workerConfig                  = @($ResourceSettings.WorkerConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs);
        workerLogLevel                = $ResourceSettings.WorkerLogLevel;
        workerMinInstances            = $ResourceSettings.WorkerMinInstances;
        workerSku                     = $ResourceSettings.WorkerSku;
        workerZipUrl                  = $workerZipUrl;
    }

    if ($ResourceSettings.ExistingWebsitePlanId) {
        $parameters.WebsitePlanId = $ResourceSettings.ExistingWebsitePlanId
    }
    else {
        $parameters.WebsitePlanName = $ResourceSettings.WebsitePlanName
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

function Get-DeploymentLocals($DeploymentLabel, $DeploymentDir) {
    if (!$DeploymentLabel) {
        $DeploymentLabel = (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss")
        Write-Status "Using deployment label: $DeploymentLabel"
    }

    if (!$DeploymentDir) {
        $DeploymentDir = Join-Path $PSScriptRoot "../../artifacts/deploy"
        Write-Status "Using deployment directory: $DeploymentDir"
    }

    return $DeploymentLabel, $DeploymentDir
}

# Source: https://stackoverflow.com/a/57599481
if (Get-TypeData -TypeName System.Array) {
    Remove-TypeData System.Array
}
