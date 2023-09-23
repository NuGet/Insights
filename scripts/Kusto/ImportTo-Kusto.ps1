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
    [ValidateSet(
        "CatalogLeafItems",
        "Certificates",
        "NuGetPackageExplorerFiles",
        "NuGetPackageExplorers",
        "PackageArchiveEntries",
        "PackageArchives",
        "PackageAssemblies",
        "PackageAssets",
        "PackageCertificates",
        "PackageCompatibilities",
        "PackageContents",
        "PackageDeprecations",
        "PackageDownloads",
        "PackageDownloadHistories",
        "PackageIcons",
        "PackageLicenses",
        "PackageManifests",
        "PackageOwners",
        "PackageReadmes",
        "PackageSignatures",
        "PackageVersions",
        "PackageVulnerabilities",
        "SymbolPackageArchiveEntries",
        "SymbolPackageArchives",
        "VerifiedPackages")]
    [string]$TableName,

    [Parameter(Mandatory = $false)]
    [string]$TableNamePrefix,

    [Parameter(Mandatory = $false)]
    [string]$TableNameSuffix,

    [Parameter(Mandatory = $false)]
    [string]$TableFolder,

    [Parameter(Mandatory = $false)]
    [string]$WorkingDirectory,

    [Parameter(Mandatory = $false)]
    [switch]$Parallel
)

$tableNameToContainerName = @{
    "CatalogLeafItems"            = "catalogleafitems";
    "Certificates"                = "certificates";
    "NuGetPackageExplorerFiles"   = "nugetpackageexplorerfiles";
    "NuGetPackageExplorers"       = "nugetpackageexplorer";
    "PackageArchiveEntries"       = "packagearchiveentries";
    "PackageArchives"             = "packagearchives";
    "PackageAssemblies"           = "packageassemblies";
    "PackageAssets"               = "packageassets";
    "PackageCertificates"         = "packagecertificates";
    "PackageCompatibilities"      = "packagecompatibilities";
    "PackageContents"             = "packagecontents";
    "PackageDeprecations"         = "packagedeprecations";
    "PackageDownloads"            = "packagedownloads";
    "PackageDownloadHistories"    = "packagedownloadhistories";
    "PackageIcons"                = "packageicons";
    "PackageLicenses"             = "packagelicenses";
    "PackageManifests"            = "packagemanifests";
    "PackageOwners"               = "packageowners";
    "PackageReadmes"              = "packagereadmes";
    "PackageSignatures"           = "packagesignatures";
    "PackageVersions"             = "packageversions";
    "PackageVulnerabilities"      = "packagevulnerabilities";
    "SymbolPackageArchiveEntries" = "symbolpackagearchiveentries";
    "SymbolPackageArchives"       = "symbolpackagearchives";
    "VerifiedPackages"            = "verifiedpackages";
}

if ($TableName -and !$tableNameToContainerName[$TableName]) {
    Write-Error "Table $TableName is not recognized"
}

if (!$WorkingDirectory) {
    $WorkingDirectory = $PSScriptRoot
}

if (!$ModelsPath) {
    $ModelsPath = [System.IO.Path]::GetFullPath((Join-Path $WorkingDirectory "../../src/Worker.Logic/Generated"))
}

$kustoConnection = "https://$KustoClusterName.kusto.windows.net;Fed=true"
$storageBaseUrl = "https://$StorageAccountName.blob.core.windows.net/"

$toolsDir = Join-Path $WorkingDirectory ".tools"
$nuget = Join-Path $toolsDir "nuget.exe"
$kustoToolsVersion = "5.2.0"
$kustoCli = Join-Path $toolsDir "Microsoft.Azure.Kusto.Tools.$kustoToolsVersion\tools\Kusto.Cli.exe"
$lightIngest = Join-Path $toolsDir "Microsoft.Azure.Kusto.Tools.$kustoToolsVersion\tools\LightIngest.exe"

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
    & $nuget install Microsoft.Azure.Kusto.Tools -Version $kustoToolsVersion -OutputDirectory $toolsDir
}

if ($Parallel) {
    if ($TableName) {
        throw "The -Parallel switch cannot be used with -TableName."
    }
    $tableNameToContainerName.Keys | ForEach-Object {
        Start-Job `
            -Name $_ `
            -FilePath $PSCommandPath `
            -ArgumentList $KustoClusterName, $KustoDatabaseName, $StorageAccountName, $StorageSas, $ModelsPath, $_, $TableNamePrefix, $TableNameSuffix, $TableFolder, $WorkingDirectory
    }

    Write-Host ""

    try {
        while (Get-Job -State "Running") {
            Get-Job | Receive-Job
            Start-Sleep 5
        }
        Get-Job | Receive-Job
    }
    finally {
        Remove-Job *
    }

    exit
}

if (!$StorageSas) {
    Write-Host "No storage SAS was provided. Fetching one using the az CLI."
    $StorageSas = az storage account generate-sas `
        --account-name $StorageAccountName `
        --services b `
        --resource-types co `
        --permissions lr `
        --expiry ((Get-Date).ToUniversalTime().AddDays(1).ToString("yyyy-MM-dd'T'HH:mm'Z'")) `
        --output tsv `
        --only-show-errors
}

$StorageSas = "?" + $StorageSas.TrimStart("?")

Write-Host "Enumerating available containers using the az CLI."
$containers = az storage container list `
    --account-name $StorageAccountName `
    --query "[].name" `
    --output tsv `
    --only-show-errors
if ($LASTEXITCODE) {
    throw "Enumerating containers in the storage account failed."
}

$models = Get-ChildItem (Join-Path $ModelsPath "*.ICsvRecord.cs") -Recurse
foreach ($model in $models) {
    $content = Get-Content $model -Raw
    $kustoDDLMatch = [Regex]::Match($content, "/\* Kusto DDL:(.+?)\*/", [Text.RegularExpressions.RegexOptions]::Singleline)
    $kustoDDL = $kustoDDLMatch.Groups[1].Value

    $tableNameMatch = [Regex]::Match($kustoDDL, "\.drop table ([^ ;]+)", [Text.RegularExpressions.RegexOptions]::Singleline)
    $foundTableName = $tableNameMatch.Groups[1].Value
    if (!$tableNameToContainerName[$foundTableName]) {
        Write-Warning "Skipping unmapped table $foundTableName."
        continue
    }

    if ($TableName -and $foundTableName -ne $TableName) {
        Write-Warning "Skipping undesired table $foundTableName."
        continue
    }

    $containerName = $tableNameToContainerName[$foundTableName]
    $selectedTableName = "$TableNamePrefix$foundTableName$TableNameSuffix"

    # Check if the container exists
    if (!($containerName -in $containers)) {
        Write-Warning "Skipping missing storage container $containerName in storage account $StorageAccountName."
        continue
    }

    # Create a temp table for the import.
    $tempTableName = "$($selectedTableName)_Temp"
    $escapedTableFolder = $TableFolder | ConvertTo-Json
    $commands = $kustoDDL
    $commands = $commands.Replace("folder = `"`"", "folder = " + $escapedTableFolder)
    $commands = [Regex]::Replace($commands, "([^\w])$foundTableName([^\w])", "`$1$tempTableName`$2")
    $commands = $commands.Replace("/$tempTableName.md", "/$foundTableName.md")
    $commands = [Regex]::Split($commands, "; *`r?`n", [Text.RegularExpressions.RegexOptions]::Singleline) `
    | ForEach-Object { [Regex]::Replace($_.Trim(), "`r?`n", " &`r`n") }
    $commands = $commands -join "`r`n"

    $script = Join-Path $toolsDir "script_$selectedTableName.kql"
    $commands | Out-File $script -Encoding UTF8
    
    $kustoCliConnection = "$kustoConnection;Initial Catalog=$KustoDatabaseName"
    & $kustoCli $kustoCliConnection -script:$script
    if ($LASTEXITCODE) {
        throw "Running Kusto.Cli to initialize the temp table failed."
    }

    # Import the data into the temp table
    $sourceUrl = "$($storageBaseUrl.TrimEnd('/'))/$containerName$StorageSas"
    $lightIngestConnection = $kustoConnection.Replace("https://", "https://ingest-")
    "" | & $lightIngest $lightIngestConnection `
        -database:$KustoDatabaseName `
        -table:$tempTableName `
        -source:$sourceUrl `
        -pattern:"*.csv.gz" `
        -format:csv `
        -mappingRef:"BlobStorageMapping" `
        -ignoreFirstRow:true
    if ($LASTEXITCODE) {
        throw "Running LightIngest to import data failed."
    }

    # Swap the temp table with the existing table
    $oldTableName = "$($selectedTableName)_Old"
    & $kustoCli $kustoCliConnection `
        -execute:".rename tables $oldTableName=$selectedTableName ifexists, $selectedTableName=$tempTableName" `
        -execute:".drop table $oldTableName ifexists"
    if ($LASTEXITCODE) {
        throw "Running Kusto.Cli to swap tables failed."
    }
}
