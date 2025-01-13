[CmdletBinding()]
param (
  [Parameter(Mandatory = $false)]
  [ValidateSet("win-x64", "linux-x64", "osx-x64")]
  [string]$RuntimeIdentifier,
  
  [Parameter(Mandatory = $false)]
  [string]$OutputPath,
  
  [Parameter(Mandatory = $false)]
  [switch]$SkipZip
)

Import-Module (Join-Path $PSScriptRoot "scripts/NuGet.Insights.psm1") -Force
$RuntimeIdentifier = Get-DefaultRuntimeIdentifier $RuntimeIdentifier

$hostRepo = "https://github.com/Azure/azure-functions-host.git"
$hostVersion = "v4.37.0"

$artifactsDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../artifacts/azure-functions"))
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

Write-Host "Cloning Azure Functions host source code"
git clone --depth 1 --branch $hostVersion $hostRepo $hostSrcDir

# Build and publish the host
$hostProjectPath = Join-Path $hostSrcDir "src/WebJobs.Script.WebHost/WebJobs.Script.WebHost.csproj"

$Env:PublishWithAspNetCoreTargetManifest = "false"

# Clear repo-level NuGet and MSBuild settings so that the host publish step is isolated.
Write-Host "Resetting repository level settings"
$submoduleDir = Resolve-Path (Join-Path $PSScriptRoot "../submodules")
$resetFiles = ".editorconfig", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "NuGet.config"
$resetFiles | ForEach-Object { Copy-Item (Join-Path $submoduleDir $_) (Join-Path $artifactsDir $_) }

Write-Host "Copying repository level NuGet.config into the host source directory"
Copy-Item (Resolve-Path (Join-Path $PSScriptRoot "../NuGet.config")) (Resolve-Path (Join-Path $hostSrcDir "NuGet.config")) -Force

Write-Host "Publishing host"
dotnet restore $hostProjectPath --verbosity Normal
dotnet publish $hostProjectPath -c Release --output $hostBinDir --framework "net8.0" --runtime $RuntimeIdentifier --self-contained false

if ($LASTEXITCODE -ne 0) {
  throw "Failed to publish the Azure Functions Host."
}

# Delete all out-of-process (non-.NET) workers to make the package smaller.
$workersDir = Join-Path $hostBinDir "workers"
Remove-DirSafe $workersDir
New-Item $workersDir -ItemType Directory | Out-Null

# Zip the host and app for a stand-alone Azure Functions deployment.
if (!$SkipZip) {
  Write-Host "Zipping host to `"$hostBinZip`""
  if (Test-Path $hostBinZip) { Remove-Item $hostBinZip }
  Compress-Archive -Path (Join-Path $hostBinDir "*") -DestinationPath $hostBinZip
}

# Delete the source directory since we don't need it anymore
Remove-DirSafe $hostSrcDir
