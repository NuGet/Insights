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

class ResourceSettings {
    [ValidatePattern("^[a-z0-9]+$")]
    [ValidateLength(1, 19)] # 19 because storage accounts and Key Vaults have max 24 characters and the prefix is "expkg".
    [ValidateNotNullOrEmpty()]
    [string]$StackName

    [ValidateNotNullOrEmpty()]
    [string]$Location
    
    [ValidateNotNullOrEmpty()]
    [string]$WebsiteName
    
    [ValidateSet("Y1", "S1", "P1v2")]
    [string]$WorkerSku

    [ValidateRange(1, 20)]
    [ValidateNotNullOrEmpty()]
    [int]$WorkerCount
    
    [ValidateSet("Information", "Warning")]
    [string]$WorkerLogLevel

    [ValidateNotNullOrEmpty()]
    [Hashtable]$WebsiteConfig
    
    [ValidateNotNullOrEmpty()]
    [Hashtable]$WorkerConfig
    
    [string]$ExistingWebsitePlanId

    [ValidateNotNullOrEmpty()]
    [string]$ResourceGroupName
    
    [ValidateNotNullOrEmpty()]
    [string]$StorageAccountName
    
    [ValidateNotNullOrEmpty()]
    [string]$KeyVaultName
    
    [ValidateNotNullOrEmpty()]
    [string]$AadAppName
    
    [ValidateNotNullOrEmpty()]
    [string]$SasConnectionStringSecretName
    
    [ValidateNotNullOrEmpty()]
    [string]$SasDefinitionName
    
    [ValidateNotNullOrEmpty()]
    [string]$DeploymentContainerName
    
    [ValidateNotNullOrEmpty()]
    [string]$LeaseContainerName
    
    [ValidateNotNullOrEmpty()]
    [TimeSpan]$SasValidityPeriod
    
    [ValidateNotNullOrEmpty()]
    [string]$WorkerNamePrefix
    
    [ValidateNotNullOrEmpty()]
    [bool]$AutoRegenerateStorageKey

    ResourceSettings(
        [string]$StackName,
        [string]$Location,
        [string]$WebsiteName,
        [string]$ExistingWebsitePlanId,
        [Hashtable]$WebsiteConfig,
        [string]$WorkerSku,
        [int]$WorkerCount,
        [string]$WorkerLogLevel,
        [Hashtable]$WorkerConfig) {

        $this.StackName = $StackName

        $this.ExistingWebsitePlanId = $ExistingWebsitePlanId
        $this.WebsiteConfig = $WebsiteConfig
        $this.WorkerConfig = $WorkerConfig
        
        $this.Location = if (!$Location) { "West US 2" } else { $Location }
        $this.WebsiteName = if (!$WebsiteName) { "ExplorePackages-$StackName" } else { $WebsiteName }
        $this.WorkerSku = if (!$WorkerSku) { "Y1" } else { $WorkerSku } 
        $this.WorkerCount = if (!$WorkerCount) { 1 } else { $WorkerCount }
        $this.WorkerLogLevel = if (!$WorkerLogLevel) { "Warning" } else { $WorkerLogLevel } 

        $this.ResourceGroupName = "ExplorePackages-$StackName"
        $this.StorageAccountName = "expkg$($StackName.ToLowerInvariant())"
        $this.KeyVaultName = "expkg$($StackName.ToLowerInvariant())"
        $this.AadAppName = "ExplorePackages-$StackName-Website"
        $this.SasConnectionStringSecretName = "$($this.StorageAccountName)-SasConnectionString"
        $this.SasDefinitionName = "BlobQueueTableFullAccessSas"
        $this.DeploymentContainerName = "deployment"
        $this.LeaseContainerName = "leases"
        $this.SasValidityPeriod = New-TimeSpan -Days 6
        $this.WorkerNamePrefix = "ExplorePackages-$StackName-Worker-"

        # Set up some default config based on worker SKU
        if ($this.WorkerSku -eq "Y1") {
            if ("NuGetPackageExplorerToCsv" -notin $this.WorkerConfig["Knapcode.ExplorePackages"].DisabledDrivers) {
                # Default "MoveTempToHome" to be true when NuGetPackageExplorerToCsv is enabled. We do this because the NuGet
                # Package Explorer symbol validation APIs are hard-coded to use TEMP and can quickly fill up the small TEMP
                # capacity on consumption plan (~500 MiB). Therefore, we move TEMP to HOME at the start of the process. HOME
                # points to a Azure Storage File share which has no capacity issues.
                if ($null -eq $this.WorkerConfig["Knapcode.ExplorePackages"].MoveTempToHome) {
                    $this.WorkerConfig["Knapcode.ExplorePackages"].MoveTempToHome = $true
                }

                # Default the maximum number of workers per Function App plan to 16 when NuGetPackageExplorerToCsv is enabled.
                # We do this because it's easy for a lot of Function App workers to overload the HOME directory which is backed
                # by an Azure Storage File share.
                if ($null -eq $this.WorkerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT) {
                    $this.WorkerConfig.WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT = 16
                }

                # Default the storage queue trigger batch size to 1 when NuGetPackageExplorerToCsv is enabled. We do this to
                # eliminate the parallelism in the worker process so that we can easily control the number of total parallel
                # queue messages are being processed and therefore are using the HOME file share.
                if ($null -eq $this.WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize) {
                    $this.WorkerConfig.AzureFunctionsJobHost__extensions__queues__batchSize = 1
                }
            }
            
            # Since Consumption plan requires WEBSITE_CONTENTAZUREFILECONNECTIONSTRING and this does not support SAS-based
            # connection strings, don't auto-regenerate in this case. We would need to regularly update a connection string based
            # on the active storage access key, which isn't worth the effort for this approach that is less secure anyway.
            $this.AutoRegenerateStorageKey = $false
        }
        else {
            $this.AutoRegenerateStorageKey = $true
        }
    }
}

# Source: https://stackoverflow.com/a/57599481
if (Get-TypeData -TypeName System.Array) {
    Remove-TypeData System.Array
}
