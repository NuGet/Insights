﻿[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$RootDirectory,

    [Parameter(Mandatory = $false)]
    [string[]]$Files
)

if (!$RootDirectory) {
    $RootDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}

$configPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../markdown-link-check.config.json"))

npm list -g markdown-link-check | Out-Null
if ($LASTEXITCODE) {
    Write-Host "The markdown-link-check global npm tool is not installed. Run this: npm install -g markdown-link-check" -ForegroundColor Red
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
    Where-Object { !$_.StartsWith((Join-Path $RootDirectory "submodules")) } | `
    Where-Object { !$_.StartsWith((Join-Path $RootDirectory "artifacts")) }

foreach ($md in $documents) {
    $failed = $true
    for ($i = 0; $i -lt 5 -and $failed; $i++) {
        $prefix = if ($i -eq 0) { "" } else { "[Attempt $($i + 1)] " }
        Write-Host "$($prefix)Checking $md" -ForegroundColor DarkGray
        $output = markdown-link-check $md --config $configPath --verbose 2>&1 | Out-String
        $statusMatches = [Regex]::Matches($output, "^\s*\[([^\]]+)\]", [System.Text.RegularExpressions.RegexOptions]::Multiline)
        $nonSuccessStatuses = $statusMatches | `
            ForEach-Object { $_.Groups[1].Value } | `
            Where-Object { $_ -ne "✓" } | `
            Where-Object { $_ -ne "/" } | `
            Sort-Object | `
            Get-Unique
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
