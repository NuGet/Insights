[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$RootDirectory,

    [Parameter(Mandatory = $false)]
    [string[]]$Files
)

if (!$RootDirectory) {
    $RootDirectory = Join-Path $PSScriptRoot ".."
}

$configPath = Join-Path $PSScriptRoot "../markdown-link-check.config.json"

npm list -g markdown-link-check | Out-Null
if ($LASTEXITCODE) {
    Write-Host "Installing markdown-link-check."
    npm install -g markdown-link-check
}

$originalOutputEncoding = [Console]::OutputEncoding
trap { [Console]::OutputEncoding = $originalOutputEncoding }
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$hasError = @()

if ($Files -and $Files.Length -gt 0) {
    $documents = $Files
}
else {
    $documents = Get-ChildItem (Join-Path $RootDirectory "*.md") -Recurse
}

foreach ($md in $documents) {
    Write-Host "Checking $md" -ForegroundColor DarkGray
    $output = markdown-link-check $md --config $configPath --verbose 2>&1 | Out-String
    $statusMatches = [Regex]::Matches($output, "^\s*\[([^\]]+)\]", [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $nonSuccessStatuses = $statusMatches | `
        ForEach-Object { $_.Groups[1].Value } | `
        Where-Object { $_ -ne "✓" } | `
        Where-Object { $_ -ne "/" } | `
        Sort-Object | `
        Get-Unique

    if (($LASTEXITCODE -ne 0) -or ($nonSuccessStatuses.Count -gt 0)) {
        # Run again for full colored and Unicode output
        markdown-link-check $md --config $configPath

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
