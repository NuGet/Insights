[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$KustoClusterName,
    
    [Parameter(Mandatory = $true)]
    [string]$KustoDatabaseName,
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountName,
    
    [Parameter(Mandatory = $false)]
    [string]$StorageSas,

    [Parameter(Mandatory = $false)]
    [string]$ModelsPath,

    [Parameter(Mandatory = $false)]
    [ValidateSet("JverCatalogLeafItems", "JverPackageAssemblies", "JverPackageAssets", "JverPackageDownloads", "JverPackageOwners", "JverPackageSignatures")]
    [string]$ImportTableName
)

$ErrorActionPreference = "Stop"

$tableNameToContainerName = @{
    "JverCatalogLeafItems" = "catalogleafitems";
    "JverPackageAssemblies" = "packageassemblies";
    "JverPackageAssets" = "packageassets";
    "JverPackageDownloads" = "packagedownloads";
    "JverPackageOwners" = "packageowners";
    "JverPackageSignatures" = "packagesignatures";
}

if (!$tableNameToContainerName[$ImportTableName]) {
    Write-Error "Table $ImportTableName is not recognized"
}

if (!$ModelsPath) {
    $ModelsPath = Join-Path $PSScriptRoot "..\..\src\ExplorePackages.Worker.Logic\Generated"
}

if (!$StorageSas) {
    Write-Host "No storage SAS was provided. Fetching one using the az CLI."
    $StorageSas = az storage account generate-sas `
        --account-name $StorageAccountName `
        --services b `
        --resource-types co `
        --permissions lr `
        --expiry ((Get-Date).ToUniversalTime().AddDays(1).ToString("yyyy-MM-dd'T'HH:mm'Z'")) `
        --output tsv
}

$StorageSas = "?" + $StorageSas.TrimStart("?")

$kustoConnection = "https://$KustoClusterName.kusto.windows.net;Fed=true"
$storageBaseUrl = "https://$StorageAccountName.blob.core.windows.net/"

$toolsDir = Join-Path $PSScriptRoot ".tools"
$nuget = Join-Path $toolsDir "nuget.exe"
$kustoCli = Join-Path $toolsDir "Microsoft.Azure.Kusto.Tools.5.0.8\tools\Kusto.Cli.exe"
$lightIngest = Join-Path $toolsDir "Microsoft.Azure.Kusto.Tools.5.0.8\tools\LightIngest.exe"

if (!(Test-Path $toolsDir)) {
    New-Item -Type Directory $toolsDir | Out-Null
}

if (!(Test-Path $nuget)) {
    $before = $ProgressPreference
    Write-Host "Downloading nuget.exe..."
    $ProgressPreference = "SilentlyContinue"
    Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $nuget
    $ProgressPreference = $before
}

if (!(Test-Path $kustoCli) -or !(Test-Path $lightIngest)) {
    & $nuget install Microsoft.Azure.Kusto.Tools -Version 5.0.8 -OutputDirectory $toolsDir
}

$models = Get-ChildItem (Join-Path $ModelsPath "*.ICsvRecord.cs") -Recurse
foreach ($model in $models) {
    $content = Get-Content $model -Raw
    $matches = [Regex]::Match($content, "/\* Kusto DDL:(.+?)\*/", [Text.RegularExpressions.RegexOptions]::Singleline)
    $kustoDDL = $matches.Groups[1].Value

    $matches = [Regex]::Match($kustoDDL, "\.drop table ([^ ;]+)", [Text.RegularExpressions.RegexOptions]::Singleline)
    $tableName = $matches.Groups[1].Value
    if (!$tableNameToContainerName[$tableName]) {
        Write-Warning "Skipping unmapped table $tableName."
        continue
    }

    if ($ImportTableName -ne $null -and $tableName -ne $ImportTableName) {
        Write-Warning "Skipping undesired table $tableName."
        continue
    }

    $containerName = $tableNameToContainerName[$tableName]

    # First, create a temp table for the import.
    $tempTableName = "$($tableName)_Temp"
    $commands = [Regex]::Replace($kustoDDL, "([^\w])$tableName([^\w])", "`$1$tempTableName`$2")
    $commands = [Regex]::Split($commands, "; *`r?`n", [Text.RegularExpressions.RegexOptions]::Singleline) `
        | ForEach-Object { [Regex]::Replace($_.Trim(), "`r?`n", " &`r`n") }
    $commands = $commands -join "`r`n"

    $script = Join-Path $toolsDir "script.kql"
    $commands | Out-File $script -Encoding UTF8
    
    $kustoCliConnection = "$kustoConnection;Initial Catalog=$KustoDatabaseName"
    & $kustoCli $kustoCliConnection -script:$script
    if ($LASTEXITCODE) {
        throw "Running Kusto.Cli to initialize the temp table failed."
    }

    # Second, import the data into the temp table
    $sourceUrl = "$($storageBaseUrl.TrimEnd('/'))/$containerName$StorageSas"
    $lightIngestConnection = $kustoConnection.Replace("https://", "https://ingest-")
    "" | & $lightIngest $lightIngestConnection -database:$KustoDatabaseName -table:$tempTableName -source:$sourceUrl -pattern:"*.csv.gz" -format:csv -mappingRef:"$($tableName)_mapping"
    if ($LASTEXITCODE) {
        throw "Running LightIngest to import data failed."
    }

    # Third, swap the temp table with the existing table
    $oldTableName = "$($tableName)_Old"
    & $kustoCli $kustoCliConnection -execute:".rename tables $oldTableName=$tableName ifexists, $tableName=$tempTableName" -execute:".drop table $oldTableName ifexists"
    if ($LASTEXITCODE) {
        throw "Running Kusto.Cli to swap tables failed."
    }
}
