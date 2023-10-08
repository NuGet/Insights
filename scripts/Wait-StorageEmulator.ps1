[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$ServerPattern = "Azurite-*",

    [Parameter(Mandatory = $false)]
    [int]$WaitSeconds = 30
)

$start = Get-Date
$attempt = 0
while ($true) {
    $ready = $true
    $attempt++
    foreach ($port in (10000 .. 10002)) {
        $url = "http://127.0.0.1:$port/"
        try {
            $response = Invoke-WebRequest $url -TimeoutSec 5
        }
        catch {
            $response = $_.Exception.Response
            $status = $response.StatusCode
            if ($response.Headers) { $server = $response.Headers.Server }
            if (!$status) {
                $ready = $false
                Write-Host "[Storage not ready] $url - $($_.Exception.Message)"
            }
            elseif (!($status -eq 400 -or ($status -ge 200 -and $status -lt 300))) {
                $ready = $false
                Write-Host "[Storage not ready] $url - HTTP $status"
            }
            elseif ($server -notlike "Azurite-*") {
                $ready = $false
                Write-Host "[Storage not ready] $url - Unexpected Server header $server"
            }
            else {
                Write-Host "[Storage ready] $url - Server header $server"
            }
        }

        if (!$ready) {
            break
        }
    }

    $duration = (Get-Date) - $start
    if ($ready) {
        Write-Host "All storage endpoints ready after $duration" -ForegroundColor Green
        break
    }
    elseif ($duration -gt [TimeSpan]::FromSeconds($WaitSeconds)) {
        Write-Error "Storage not ready after $duration"
        exit 1
    }
    else {
        $remaining = [int][Math]::Ceiling([Math]::Min($WaitSeconds - $duration.TotalSeconds, $attempt))
        if ($remaining -gt 0) {
            Write-Host "Waiting $remaining second(s)"
            Start-Sleep -Seconds $remaining
        }
    }
}
