[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$StampName,

    [switch]$Undo
)

dynamicparam {
    Import-Module (Join-Path $PSScriptRoot "../deploy/scripts/NuGet.Insights.psm1") -Force
    
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
    Import-Module (Join-Path $PSScriptRoot "../deploy/scripts/NuGet.Insights.psm1") -Force

    $runtimeIdentifier = Get-DefaultRuntimeIdentifier $null $false
    $resourceSettings = Get-ResourceSettings $ConfigName $StampName $runtimeIdentifier

    $currentUser = Get-AzCurrentUser
    $roles = "Storage Blob Data Contributor", "Storage Queue Data Contributor", "Storage Table Data Contributor"

    if (!$Undo) {
        Write-Status "Disabling storage firewall..."
        Set-StorageFirewallDefaultAction $ResourceSettings "Allow"
    
        $roles | ForEach-Object { Add-AzRoleAssignmentWithRetry $currentUser $ResourceSettings.ResourceGroupName $_ {} 0 }
    } else {
        Write-Status "Enabling storage firewall..."
        Set-StorageFirewallDefaultAction $ResourceSettings "Deny"
    
        $roles | ForEach-Object { Remove-AzRoleAssignmentWithRetry $currentUser $ResourceSettings.ResourceGroupName $_ -AllowMissing }
    }
}