[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$StampName,

    [Parameter(Mandatory = $false)]
    [string]$WorkQueueName,

    [Parameter(Mandatory = $false)]
    [string]$ExpandQueueName,

    [Parameter(Mandatory = $false)]
    [string]$LogLevel = "Information",

    [Parameter(Mandatory = $false)]
    [bool]$RestrictUsers = $false,

    [Parameter(Mandatory = $false)]
    [bool]$AddCurrentUserToStorage = $true,

    [Parameter(Mandatory = $false)]
    [bool]$DisableMetricsFunction = $true,

    [Parameter(Mandatory = $false)]
    [bool]$DisableTimerFunction = $true,

    [Parameter(Mandatory = $false)]
    [bool]$SingleQueueMessage = $true,

    [Parameter(Mandatory = $false)]
    [switch]$Undo
)

dynamicparam {
    Import-Module (Join-Path $PSScriptRoot "../deploy/scripts/NuGet.Insights.psm1") -Force
    
    $ConfigNameKey = "ConfigName"
    $configNamesParameter = Get-ConfigNameDynamicParameter ([string[]]) $ConfigNameKey

    $parameterDictionary = [System.Management.Automation.RuntimeDefinedParameterDictionary]::new()
    $parameterDictionary.Add($ConfigNameKey, $configNamesParameter)
    return $parameterDictionary
}

begin {
    $ConfigName = $PsBoundParameters[$ConfigNameKey]
}

process {
    Import-Module (Join-Path $PSScriptRoot "../deploy/scripts/NuGet.Insights.psm1") -Force

    $websiteConfigPath = Resolve-Path (Join-Path $PSScriptRoot "../src/Website/appsettings.Development.json")
    $workerConfigPath = Resolve-Path (Join-Path $PSScriptRoot "../src/Worker/local.settings.json")
    $toolConfigPath = Resolve-Path (Join-Path $PSScriptRoot "../src/Tool/appsettings.Development.json")
    $workerHostPath = Resolve-Path (Join-Path $PSScriptRoot "../src/Worker/host.json")
    $AddCurrentUserToStoragePath = Resolve-Path (Join-Path $PSScriptRoot "Add-CurrentUserToStorage.ps1")

    if (!$Undo) {
        $runtimeIdentifier = Get-DefaultRuntimeIdentifier $null $false
        $baselineResourceSettings = Get-ResourceSettings $ConfigName $StampName $runtimeIdentifier

        $resourceSettings = Get-ResourceSettings $ConfigName $StampName $runtimeIdentifier (@{
                Deployment  = @{
                    WorkerLogLevel  = $LogLevel;
                    WorkQueueName   = if ($WorkQueueName) { $WorkQueueName } else { $baselineResourceSettings.WorkQueueName };
                    ExpandQueueName = if ($ExpandQueueName) { $ExpandQueueName } else { $baselineResourceSettings.ExpandQueueName };
                };
                AppSettings = @{
                    Website = @{
                        NuGetInsights = @{
                            RestrictUsers = $RestrictUsers;
                        };
                    };
                    Worker  = @{
                        NUGET_INSIGHTS_ALLOW_ICU                = "true"
                        "AzureWebJobs.MetricsFunction.Disabled" = $DisableMetricsFunction
                        "AzureWebJobs.TimerFunction.Disabled"   = $DisableTimerFunction
                    }
                };
            })
    
        # website config
        $websiteConfig = $ResourceSettings.WebsiteConfig | Get-OrderedHashtable | ConvertTo-Json -Depth 100 | Format-Json
        Write-Status "Writing website config to $websiteConfigPath"
        $websiteConfig | Out-File $websiteConfigPath -Encoding utf8
        
        # worker config
        $singleQueueMessageConfig = @{}
        if ($SingleQueueMessage) {
            $singleQueueMessageConfig = @{
                extensions = @{
                    queues = @{
                        batchSize         = 1;
                        newBatchThreshold = 0;
                    };
                };
            }
        }

        $workerConfig = $ResourceSettings.WorkerConfig
        if ($SingleQueueMessage) {
            $workerConfig = Merge-Hashtable $workerConfig (@{
                    AzureFunctionsJobHost = $singleQueueMessageConfig;
                })
        }

        Write-Status "Writing tool config to $toolConfigPath"
        $workerConfig | Get-OrderedHashtable | ConvertTo-Json -Depth 100 | Format-Json | Out-File $toolConfigPath -Encoding utf8

        $flatWorkerConfig = $workerConfig | ConvertTo-FlatConfig
        $workerConfig = (@{
                IsEncrypted = $false;
                Values      = $flatWorkerConfig;
            }) | Get-OrderedHashtable | ConvertTo-Json -Depth 100 | Format-Json
        Write-Status "Writing worker config to $workerConfigPath"
        $workerConfig | Out-File $workerConfigPath -Encoding utf8

        # worker host.json
        if ($SingleQueueMessage) {
            $workerHostConfig = Get-Content $workerHostPath -Raw | ConvertFrom-Json | ConvertTo-Hashtable
            $workerHostConfig = Merge-Hashtable $workerHostConfig $singleQueueMessageConfig | Get-OrderedHashtable | ConvertTo-Json -Depth 100 | Format-Json
            Write-Status "Writing worker host.json to $workerHostPath"
            $workerHostConfig | Out-File $workerHostPath -Encoding utf8
        }
    
        if ($AddCurrentUserToStorage) {
            & $AddCurrentUserToStoragePath -ConfigName $ConfigName -StampName $StampName
        }
    }
    else {
        Write-Status "Resetting $websiteConfigPath"
        git checkout $websiteConfigPath
        Write-Status "Resetting $workerConfigPath"
        git checkout $workerConfigPath
        
        if ($SingleQueueMessage) {
            Write-Status "Resetting $workerHostPath"
            git checkout $workerHostPath
        }
    
        if ($AddCurrentUserToStorage) {
            & $AddCurrentUserToStoragePath -ConfigName $ConfigName -StampName $StampName -Undo
        }
    }
}