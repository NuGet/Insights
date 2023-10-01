[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$StampName
)

dynamicparam {
    Import-Module (Join-Path $PSScriptRoot "scripts/NuGet.Insights.psm1") -Force
    
    $ConfigNameKey = "ConfigName"
    $configNamesParameter = Get-ConfigNameDynamicParameter ([string[]]) $ConfigNameKey

    $parameterDictionary = [System.Management.Automation.RuntimeDefinedParameterDictionary]::new()
    $parameterDictionary.Add($ConfigNameKey, $configNamesParameter)
    return $parameterDictionary
}

begin {
    $ConfigName = $PsBoundParameters[$ConfigNameKey]
}

process {
    Import-Module (Join-Path $PSScriptRoot "scripts/NuGet.Insights.psm1") -Force

    $runtimeIdentifier = Get-DefaultRuntimeIdentifier $null $false
    $resourceSettings = Get-ResourceSettings $ConfigName $StampName $runtimeIdentifier

    $parameters = @{ ResourceSettings = $resourceSettings }

    Write-Status "Using the following deployment parameters:"
    ConvertTo-Json $parameters -Depth 100 | Out-Default

    Approve-SubscriptionId $resourceSettings.SubscriptionId

    . (Join-Path $PSScriptRoot "scripts/Invoke-Prepare.ps1") @parameters | Out-Null
}