[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string[]]$Files,
    
    [Parameter(Mandatory = $false)]
    [string]$RootDirectory
)

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

if (!$RootDirectory) {
    $RootDirectory = $repoRoot
}

$configPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../markdown-link-check.config.json"))

if (!(Get-Command npm -ErrorAction Ignore)) {
    Write-Host "The npm command could not be found. Go install Node.js and npm." -ForegroundColor Red
    exit 1
}

npm list markdown-link-check --prefix $repoRoot | Out-Null
if ($LASTEXITCODE) {
    Write-Host "The markdown-link-check npm tool is not installed. Run this: npm ci --prefix $repoRoot" -ForegroundColor Red
    exit 1
}

$originalOutputEncoding = [Console]::OutputEncoding
trap { [Console]::OutputEncoding = $originalOutputEncoding }
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$hasError = @()

if ($Files -and $Files.Length -gt 0) {
    $documents = $Files | ForEach-Object { (Resolve-Path $_).ToString() }
}
else {
    $documents = Get-ChildItem (Join-Path $RootDirectory "*.md") -Recurse | ForEach-Object { $_.FullName }
}

$documents = $documents | `
    Where-Object { !$_.StartsWith((Join-Path $RootDirectory "node_modules")) } | `
    Where-Object { !$_.StartsWith((Join-Path $RootDirectory "submodules")) } | `
    Where-Object { !$_.StartsWith((Join-Path $RootDirectory "artifacts")) } | `
    Where-Object { !$_.StartsWith((Join-Path $RootDirectory "test/Logic.Test/TestInput/Cache")) }

foreach ($md in $documents) {
    $failed = $true
    for ($i = 0; $i -lt 5 -and $failed; $i++) {
        $prefix = if ($i -eq 0) { "" } else { "[Attempt $($i + 1)] " }
        Write-Host "$($prefix)Checking $md" -ForegroundColor DarkGray
        $output = npm exec markdown-link-check --prefix $repoRoot -- $md --config $configPath --verbose 2>&1 | Out-String
        $statusMatches = [Regex]::Matches($output, "^\s*\[(?:\x1b\[[0-9;]*m)?([^\]]+?)(?:\x1b\[[0-9;]*m)?\]", [System.Text.RegularExpressions.RegexOptions]::Multiline)
        $nonSuccessStatuses = $statusMatches | `
            ForEach-Object { $_.Groups[1].Value.Trim() } | `
            Where-Object { $_ -ne "✓" } | `
            Where-Object { $_ -ne "/" } | `
            Sort-Object | `
            Get-Unique | `
            ForEach-Object { $_ + " (" + (([System.Text.Encoding]::UTF8.GetBytes($_) | ForEach-Object ToString X2) -join ' ') + ")" }
        $failed = ($LASTEXITCODE -ne 0) -or ($nonSuccessStatuses.Count -gt 0)
    }

    if ($failed) {
        Write-Host $output

        if ($nonSuccessStatuses.Count -gt 0) {
            Write-Host "Non-success link statuses found: $nonSuccessStatuses" -ForegroundColor Yellow
        }

        $hasError += @($md)
    }
}

if ($hasError.Count -gt 0) {
    Write-Host "Invalid links found in the following files:" -ForegroundColor Red
    foreach ($md in $hasError) {
        Write-Host "  $md" -ForegroundColor Red
    }
    exit 1
}
