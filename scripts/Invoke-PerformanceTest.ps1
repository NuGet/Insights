[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$LastCommitHash
)

Import-Module (Join-Path $PSScriptRoot "../deploy/scripts/NuGet.Insights.psm1") -Force

function Get-BenchmarkLog($before, $after) {
    $allLogs = Get-ChildItem (Join-Path $PSScriptRoot "../src/Performance/BenchmarkDotNet.Artifacts") -Filter "*.log"
    $logs = $allLogs | Where-Object { $_.LastWriteTimeUtc -ge $before } | Where-Object { $_.LastAccessTimeUtc -le $after }
    if ($logs.Count -ne 1) {
        Write-Error "There should be exactly one log file between $before and $after."
        exit
    }

    return $logs[0]
}

try {
    Push-Location (Join-Path $PSScriptRoot "../src/Performance")

    $type = git cat-file -t $LastCommitHash
    if (($LASTEXITCODE -ne 0) -or ($type -ne "commit")) {
        Write-Error "Commit hash '$LastCommitHash' is not a valid commit ref."
        exit
    }

    $resolvedLastCommitHash = git rev-parse --verify $LastCommitHash
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Commit ref '$LastCommitHash' could not be resolved to a commit hash."
        exit
    }

    $currentCommit = git rev-parse --verify HEAD
    git merge-base --is-ancestor $resolvedLastCommitHash $currentCommit
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Commit '$resolvedLastCommitHash' is not an ancestor."
        exit
    }

    Write-Status "Running performance tests until the current commit hash is $resolvedLastCommitHash."
    Write-Status "Switching to 'detached HEAD' state."
    git checkout $currentCommit
    if ($LASTEXITCODE -ne 0) { Write-Error "Command 'git checkout $currentCommit' failed."; exit }

    while ($true) {
        $currentCommit = git rev-parse --verify HEAD
        Write-Status "Current commit: $($currentCommit)"

        Write-Status "Running performance tests..."
        $before = (Get-Date).ToUniversalTime()
        dotnet run --configuration Release
        $after = (Get-Date).ToUniversalTime()

        $log = Get-BenchmarkLog $before $after
        $logData = [PSCustomObject]@{ Commit = $currentCommit; LogFile = $log.FullName }
        $csvPath = Join-Path $PSScriptRoot "../src/Performance/BenchmarkDotNet.Artifacts/logs.csv"
        $logData | Export-Csv -Append -NoTypeInformation -Path $csvPath

        if ($currentCommit -eq $resolvedLastCommitHash) {
            break
        }

        git checkout HEAD~1
        if ($LASTEXITCODE -ne 0) { Write-Error "Command 'git checkout HEAD~1' failed."; exit }
    }
}
finally {
    Pop-Location
}