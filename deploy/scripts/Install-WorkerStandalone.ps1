[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$BuildVersion,
    
    [Parameter(Mandatory = $true)]
    [string]$HostPattern,
    
    [Parameter(Mandatory = $true)]
    [string]$AppPattern,
    
    [Parameter(Mandatory = $true)]
    [string]$EnvPattern,

    [Parameter(Mandatory = $true)]
    [int]$LocalHealthPort,
    
    [Parameter(Mandatory = $true)]
    [string]$ApplicationInsightsInstrumentationKey,

    [Parameter(Mandatory = $true)]
    [string]$UserManagedIdentityClientId,

    [Parameter(Mandatory = $false)]
    [switch]$ExpandOSPartition
)

$ErrorActionPreference = "Stop"

$dotnetInstallUrl = "https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.ps1"
$dotnetChannel = "6.0"
$dotnetRuntime = "aspnetcore"
$binDir = "C:\bin"
$dotnetInstallPath = Join-Path $binDir "dotnet-install.ps1"
$dotnetDir = Join-Path $binDir "Microsoft\dotnet"
$dotnet = Join-Path $dotnetDir "dotnet.exe"
$scheduledTaskName = "NuGet.Insights Standalone Worker"
$installDir = Join-Path $binDir $BuildVersion

Function Get-Pattern($pattern) {
    if (![IO.Path]::IsPathRooted($pattern)) { $pattern = Join-Path $PSScriptRoot $pattern }
    $patternMatches = @(Get-ChildItem $pattern)
    if ($patternMatches.Count -ne 1) {
        throw "Exactly one file with pattern '$pattern' was expected. $($patternMatches.Count) were found:$([Environment]::NewLine)$($patternMatches -Join [Environment]::NewLine)"
    }
    return $patternMatches[0]
}

Function Expand-Pattern($type, $pattern) {
    $path = Get-Pattern $pattern
    Write-Host "Found $type path: $path"
    $destDir = Join-Path $installDir $type
    $destMarker = "$destDir.extracted"
    if (!(Test-Path $destMarker)) {
        if (Test-Path $destDir) { Remove-Item $destDir -Force -Recurse }
        Expand-Archive $path -DestinationPath $destDir
        "" | Out-File $destMarker -Encoding utf8
    }
    else {
        Write-Host "Marker file exists, skipping extraction."
    }

    return $destDir
}

# Download and install the .NET runtime
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
if (!(Test-Path $installDir)) { New-Item $installDir -ItemType Directory | Out-Null }
Invoke-WebRequest $dotnetInstallUrl -OutFile $dotnetInstallPath
& $dotnetInstallPath -Channel $dotnetChannel -Runtime $dotnetRuntime -InstallDir $dotnetDir -NoPath
Write-Host ""

if ($ExpandOSPartition) {
    # Expand the primary partition, if possible. The image partition size may be less than the drive size.
    $driveLetter = "C"
    $osPartition = Get-Partition -DriveLetter $driveLetter
    $osDisk = Get-Disk $osPartition.DiskNumber
    $extraSpaceMB = [int]($osDisk.LargestFreeExtent / (1024 * 1024))
    if ($extraSpaceMB -gt 0) {
        Write-Host "Expanding the $($driveLetter):\ partition by $extraSpaceMB MB"
        "select volume=C$([Environment]::NewLine)extend size=$extraSpaceMB" | diskpart
    }
}

# Extract the host and the app
$hostRoot = Expand-Pattern "host" $HostPattern
$appRoot = Expand-Pattern "app" $AppPattern
$hostPath = Join-Path $hostRoot "Microsoft.Azure.WebJobs.Script.WebHost.dll"

# The host hangs if the workers directory is not present.
$workersDir = Join-Path $hostRoot "workers"
if (!(Test-Path $workersDir)) { New-Item $workersDir -ItemType Directory | Out-Null }

Write-Host ""

# Find the environment variables file and load it, start building the app script
$envPath = Get-Pattern $EnvPattern
Write-Host "Found env path: $envPath"
$scriptEnv = [ordered]@{}
foreach ($line in Get-Content $envPath) {
    $splits = $line.Split("=", 2)
    Write-Host "Setting file env: $($splits[0])"
    $scriptEnv[$splits[0]] = $splits[1]
}
$hostEnv = [ordered]@{
    "ASPNETCORE_URLS"                                 = "http://localhost:$LocalHealthPort";
    "AzureFunctionsJobHost:Logging:Console:IsEnabled" = "false";
    "AzureWebJobsScriptRoot"                          = $appRoot;
    "NuGet.Insights:BuildVersion"                     = $BuildVersion;
    "WEBSITE_HOSTNAME"                                = "localhost:$LocalHealthPort"
    "DOTNET_gcServer"                                 = "1"
}

if ($ApplicationInsightsInstrumentationKey) {
    $hostEnv["APPINSIGHTS_INSTRUMENTATIONKEY"] = $ApplicationInsightsInstrumentationKey;
}

if ($UserManagedIdentityClientId) {
    $hostEnv["AzureWebJobsStorage:clientId"] = $UserManagedIdentityClientId;
    $hostEnv["NuGet.Insights:UserManagedIdentityClientId"] = $UserManagedIdentityClientId;
    $hostEnv["QueueTriggerConnection:clientId"] = $UserManagedIdentityClientId;
}

foreach ($pair in $hostEnv.GetEnumerator()) {
    Write-Host "Setting host env: $($pair.Key)"
    $scriptEnv[$pair.Key] = $pair.Value
}

$scriptContent = "Write-Host 'Setting environment variables'$([Environment]::NewLine)"
foreach ($pair in $scriptEnv.GetEnumerator() | Sort-Object -Property Key) {
    if ($pair.Value -eq "PLACEHOLDER") {
        throw "The environment variable '$($pair.Key)' still has the PLACEHOLDER value."
    }

    $scriptContent += "`${Env:$($pair.Key)} = '$($pair.Value.Replace("'", "''"))'$([Environment]::NewLine)"
}
Write-Host ""

$scriptPath = Join-Path $installDir "run.ps1"
$logPath = Join-Path $installDir "log.txt"
$scriptContent += "$([Environment]::NewLine)Write-Host 'Starting host'$([Environment]::NewLine)& `"$dotnet`" `"$hostPath`""

# Initialized the scheduled task and stop any existing instance
$trigger = New-ScheduledTaskTrigger -AtStartup
$action = New-ScheduledTaskAction -Execute "cmd" -Argument "/c powershell -File $scriptPath > $logPath 2>&1" -WorkingDirectory $appRoot
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserID "NT AUTHORITY\LocalService" -LogonType ServiceAccount
$existingTasks = Get-ScheduledTask -TaskName $scheduledTaskName -ErrorAction Ignore
if ($existingTasks) { 
    Write-Host "Stopping any existing scheduled tasks and waiting for dotnet stop"
    $existingTasks | Stop-ScheduledTask
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ((Get-Process -Name "dotnet" -ErrorAction Ignore) -and ($timer.Elapsed -lt [TimeSpan]::FromSeconds(10))) {
        Write-Host "." -NoNewline
        Start-Sleep -Milliseconds 500
    }
    Write-Host ""
    Write-Host ""
}

# Write out the script file
Write-Host "Writing script file"
Write-Host "dotnet: $dotnet"
Write-Host "host path: $hostPath"
Write-Host "app root: $appRoot"
Write-Host "script path: $scriptPath"
Write-Host "log path: $logPath"
$scriptContent | Out-File -FilePath $scriptPath -Encoding utf8
Write-Host ""

# Register and start the scheduled task
Register-ScheduledTask -TaskName $scheduledTaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName $scheduledTaskName

Write-Host "The script has been registered as scheduled task '$scheduledTaskName' to run on startup, and has been started."
