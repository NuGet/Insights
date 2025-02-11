[CmdletBinding()]
param ()

function Get-ReportTable($logFile) {
    $lines = Get-Content $logFile
    $state = "Start"
    $tableLines = @()
    foreach ($line in $lines) {
        if ($state -eq "Start") {
            if ($line -eq "// * Summary *") {
                $state = "FoundSummary"
            }
        }
        elseif ($state -eq "FoundSummary") {
            if ($line -like "|*") {
                $tableLines += $line
            }
            elseif ($line -like "//*") {
                break
            }
        }
    }

    return $tableLines -join [Environment]::NewLine
}

Import-Module (Join-Path $PSScriptRoot "../deploy/scripts/NuGet.Insights.psm1") -Force

$csvPath = Join-Path $PSScriptRoot "../src/Performance/BenchmarkDotNet.Artifacts/logs.csv"
if (!(Test-Path $csvPath)) {
    Write-Error "No performance logs found. Run Invoke-PerformanceTest.ps1 which produces a needed 'logs.csv' file."
    exit
}

$logRecords = Get-Content $csvPath | ConvertFrom-Csv
$reportChunks = @("# Performance report")
foreach ($record in $logRecords) {
    $commitMessage = git log --format=%B -n 1 $record.Commit
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Could not get commit message for commit $($record.Commit)."
        exit
    }
    $table = Get-ReportTable $record.LogFile

    $reportChunks += "## Commit $($record.Commit)"
    $reportChunks += "Commit message:"
    $reportChunks += "``````"
    $reportChunks += $commitMessage
    $reportChunks += "``````"
    $reportChunks += "Results:"
    $reportChunks += "``````"
    $reportChunks += $table
    $reportChunks += "``````"
}

$reportPath = Join-Path $PSScriptRoot "../src/Performance/BenchmarkDotNet.Artifacts/report.md"
$reportChunks -join [Environment]::NewLine | Out-File $reportPath -Encoding utf8
