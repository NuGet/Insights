[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [switch]$Verify
)

Push-Location
Set-Location (Join-Path $PSScriptRoot "..")
try {
    $arguments = "format", "--exclude", "src/Forks", "--exclude", "submodules"
    if ($Verify) {
        $arguments += "--verify-no-changes"
    }
    
    Write-Host "Executing: dotnet $arguments" -ForegroundColor Green
    dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Command 'dotnet $arguments' failed with exit code $LASTEXITCODE."
    }    
}
finally {
    Pop-Location
}
