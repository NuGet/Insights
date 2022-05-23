[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$RuntimeIdentifier,
  
  [Parameter(Mandatory = $false)]
  [string]$OutputPath
)

$hostVersion = "4.3.0"

$artifactsDir = Join-Path $PSScriptRoot "../artifacts/azure-functions"
$hostSrcUrl = "https://github.com/Azure/azure-functions-host/archive/v$hostVersion.zip"
$hostSrcZip = Join-Path $artifactsDir "azure-functions-host-$hostVersion.zip"
$hostSrcDir = Join-Path $artifactsDir "azure-functions-host-$hostVersion"
$hostBinDir = Join-Path $artifactsDir "host"
$hostBinZip = if ($OutputPath) { $OutputPath } else { Join-Path $artifactsDir "AzureFunctionsHost.zip" }

function Remove-DirSafe ($dir) {
  if (Test-Path $dir) {
    Write-Host "Deleting directory $dir"
    try {
      Remove-Item $dir -Recurse -Force -ErrorAction Stop
    }
    catch {
      # Remove-Item most likely fails for long path issues, which are only present on Windows. Therefore, just try to
      # use Windows cmd.exe and the built-in rmdir here.
      cmd /C "rmdir /S /Q $dir"
    }
  }
}

Remove-DirSafe $artifactsDir
New-Item $artifactsDir -ItemType Directory | Out-Null

# Download the Azure Functions host source code from GitHub
Write-Host "Downloading Azure Functions host source code"
$beforeSecurityProtocol = [Net.ServicePointManager]::SecurityProtocol;
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$beforeProgressPreference = $ProgressPreference
$ProgressPreference = "SilentlyContinue"
Invoke-WebRequest $hostSrcUrl -OutFile $hostSrcZip
[Net.ServicePointManager]::SecurityProtocol = $beforeSecurityProtocol
$ProgressPreference = $beforeProgressPreference

# Unzip the source code
Expand-Archive $hostSrcZip -DestinationPath $artifactsDir

# Build and publish the host
$hostProjectPath = Join-Path $hostSrcDir "src/WebJobs.Script.WebHost/WebJobs.Script.WebHost.csproj"

$Env:PublishWithAspNetCoreTargetManifest = "false"

# Clear repo-level NuGet and MSBuild settings so that the host publish step is isolated.
Write-Host "Resetting repository level settings"
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
  <packageSourceMapping>
    <clear />
  </packageSourceMapping>
  <disabledPackageSources>
    <clear />
  </disabledPackageSources>
</configuration>
"@ | Out-File (Join-Path $artifactsDir "NuGet.config") -Encoding UTF8

"<Project></Project>" | Out-File (Join-Path $artifactsDir ".\Directory.Build.props") -Encoding UTF8
"<Project></Project>" | Out-File (Join-Path $artifactsDir ".\Directory.Build.targets") -Encoding UTF8

Write-Host "Publishing host"
dotnet publish $hostProjectPath -c Release --output $hostBinDir --runtime $RuntimeIdentifier --self-contained false

# Delete all out-of-process (non-.NET) workers to make the package smaller.
Remove-DirSafe (Join-Path $hostBinDir "workers/*")

# Zip the host and app for a stand-alone Azure Functions deployment.
Write-Host "Zipping host to `"$hostBinZip`""
if (Test-Path $hostBinZip) { Remove-Item $hostBinZip }
Compress-Archive -Path (Join-Path $hostBinDir "*") -DestinationPath $hostBinZip

# Delete the source directory since we don't need it anymore
Remove-DirSafe $hostSrcDir
