[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$StackName,

    [Parameter(Mandatory = $false)]
    [string]$Location = "West US 2",
    
    [Parameter(Mandatory = $false)]
    [string]$ExistingWebsitePlanId,
    
    [Parameter(Mandatory = $false)]
    [int]$WorkerCount = 1,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Warning", "Information")]
    [string]$WorkerLogLevel = "Warning",

    [Parameter(Mandatory = $false)]
    [Hashtable]$WebsiteConfig = @{},

    [Parameter(Mandatory = $false)]
    [Hashtable]$WorkerConfig = @{}
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$resourceGroupName = "ExplorePackages-$StackName"
$storageAccountName = "explore$StackName"
$keyVaultName = "explore$StackName"
$aadAppName = "ExplorePackages-$StackName-Website"
$storageKeySecretName = "$storageAccountName-FullAccessConnectionString"
$sasDefinitionName = "BlobQueueTableFullAccessSas"

# Publish the projects
$deployDir = Join-Path $root "artifacts/deploy"
if (Test-Path $deployDir) {
    Remove-Item $deployDir -Recurse
}
New-Item $deployDir -Type Directory | Out-Null

function Publish-Project ($projectName) {
    Write-Status "Publishing project '$projectName'..."
    Invoke-Call { dotnet publish (Join-Path $root "src/$projectName") --configuration Release } | Out-Host
    return Join-Path $deployDir "$projectName.zip"
}

$websiteZipPath = Publish-Project ExplorePackages.Website
$workerZipPath = Publish-Project ExplorePackages.Worker

# Make sure the resource group is created
Write-Status "Ensuring the resource group '$resourceGroupName' exists..."
Invoke-Call { az group create `
    --name $resourceGroupName `
    --location $Location `
    --output tsv `
    --query 'id' }

# Make sure the storage account is created
Write-Status "Ensuring the storage account '$storageAccountName' exists..."
Invoke-Call { az storage account create `
    --name $storageAccountName `
    --resource-group $resourceGroupName `
    --location $Location `
    --kind 'StorageV2' `
    --sku 'Standard_LRS' `
    --min-tls-version 'TLS1_2' `
    --output tsv `
    --query 'id' } | Tee-Object -Variable 'storageAccountId'

# Make sure the KeyVault is created
Write-Status "Ensuring the KeyVault '$keyVaultName' exists..."
Invoke-Call { az keyvault list `
    --resource-group $resourceGroupName `
    --query "[?name=='$keyVaultName'] | length(@)" } | Tee-Object -Variable 'matchedKeyVaults'
$matchedKeyVaults = [int]$matchedKeyVaults
if ($matchedKeyVaults -eq 0) {
    Write-Status "Creating KeyVault '$keyVaultName'..."
    Invoke-Call { az keyvault create `
        --name $keyVaultName `
        --resource-group $resourceGroupName `
        --location $Location `
        --output tsv `
        --query 'id' }
}

# Get the current user
Write-Status "Determining the current user principal name for KeyVault operations..."
Invoke-Call { az ad signed-in-user show `
    --query "userPrincipalName" `
    --output tsv } | Tee-Object -Variable 'upn'

# Manage the storage account in KeyVault
& (Join-Path $PSScriptRoot "Set-KeyVaultManagedStorage.ps1") `
    -KeyVaultName $keyVaultName `
    -StorageAccountName $storageAccountName `
    -StorageAccountId $storageAccountId `
    -UserPrincipalName $upn `
    -SasDefinitionName $sasDefinitionName

# Set the currently active key in KeyVault. This is needed for Azure Functions which cannot use SAS in all places.
& (Join-Path $PSScriptRoot "Set-LatestStorageKey.ps1") `
    -KeyVaultName $keyVaultName `
    -ResourceGroupName $resourceGroupName `
    -StorageAccountName $storageAccountName `
    -StorageKeySecretName $storageKeySecretName `
    -UserPrincipalName $upn

# Deploy the server farm, if not provided
if (!$ExistingWebsitePlanId) {
    $websitePlanName = "ExplorePackages-$StackName-WebsitePlan"
    Write-Status "Ensuring the website plan '$websitePlanName'..."
    Invoke-Call { az appservice plan create `
        --name $websitePlanName `
        --resource-group $resourceGroupName `
        --location $Location `
        --sku 'B1' `
        --output tsv `
        --query 'id' } | Tee-Object -Variable 'websitePlan'
    $websitePlan = $websitePlan | ConvertFrom-Json
    $websitePlanId = $websitePlan.id
} else {
    $websitePlanId = $ExistingWebsitePlanId
}

# Initialize the AAD app
(& (Join-Path $PSScriptRoot "Initialize-AadApp.ps1") -AadAppName $AadAppName) `
    | Tee-Object -Variable 'aadApp' | Out-Host

$needsAnotherDeploy = $true
$deploymentCount = 0
while ($needsAnotherDeploy -and ($deploymentCount -lt 2)) {
    # To workaround limitations in initial deployment Azure Functions, we check the current state and may need to
    # deploy a second time.
    Write-Status "Counting existing function apps..."
    Invoke-Call { az functionapp list `
        --resource-group $resourceGroupName `
        --query "[] | length(@)" } | Tee-Object -Variable 'existingWorkerCount'
    $existingWorkerCount = [int]$existingWorkerCount

    if ($existingWorkerCount -gt $workerCount) {
        # Would need to:
        # - Delete function apps
        # - Remove managed identity from KV policy (maybe done automatically by ARM)
        # - Delete the File Share (WEBSITE_CONTENTSHARE) created by the function app
        throw 'Reducing the number of workers is not supported.'
    }

    # Deploy the rest of the resources
    $deploymentCount++
    if ($deploymentCount -eq 1) {
        Write-Status "Deploying the resources..."
        $deploymentName = "main"
    } else {
        Write-Status "Deploying again to fix up the secrets..."
        $deploymentName = "main-fix-secrets"
    }

    # Docs: https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/parameter-files
    $parameters = @{
        "`$schema" = "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#";
        contentVersion = "1.0.0.0";
        parameters = @{
            stackName = @{ value = $StackName };
            storageAccountName = @{ value = $storageAccountName };
            storageKeySecretName = @{ value = $storageKeySecretName };
            sasDefinitionName = @{ value = $sasDefinitionName };
            keyVaultName = @{ value = $keyVaultName };
            websitePlanId = @{ value = $websitePlanId };
            websiteAadClientId = @{ value = $aadApp.appId };
            websiteConfig = @{ value = $websiteConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs };
            workerConfig = @{ value = $workerConfig | ConvertTo-FlatConfig | ConvertTo-NameValuePairs };
            workerLogLevel = @{ value = $WorkerLogLevel };
            workerCount = @{ value = $workerCount };
            existingWorkerCount = @{ value = $existingWorkerCount }
        }
    }

    $parametersPath = Join-Path $deployDir "parameters.$deploymentName.json"
    $parameters | ConvertTo-Json -Depth 100 | Out-File $parametersPath -Encoding UTF8

    Invoke-Call { az deployment group create `
        --template-file (Join-Path $PSScriptRoot "../main.bicep") `
        --resource-group $resourceGroupName `
        --name $deploymentName `
        --no-prompt `
        --parameters "@$parametersPath" } | Tee-Object -Variable 'deployment'
    $deployment = $deployment | ConvertFrom-Json
    $outputs = $deployment.properties.outputs

    $needsAnotherDeploy = $outputs.needsAnotherDeploy.value
    $websiteDefaultHostName = $outputs.websiteDefaultHostName.value
    $websiteHostNames = $outputs.websiteHostNames.value
    $websiteId = $outputs.websiteId.value
    $workerIds = $outputs.workerIds.value
}

# Make the app service support the website for login
Write-Status "Enabling the AAD app for website login..."
$appServiceJson = (@{
    api = @{ requestedAccessTokenVersion = 2 };
    signInAudience = "AzureADandPersonalMicrosoftAccount";
    web = @{
        homePageUrl = "https://$($websiteDefaultHostName)";
        redirectUris = @($websiteHostNames | ForEach-Object { "https://$_/signin-oidc" })
        logoutUrl = "https://$($websiteDefaultHostName)/signout-oidc"
    }
} | ConvertTo-Json -Depth 100 -Compress).Replace('"', '\"')
Invoke-Call { az rest `
    --method PATCH `
    --headers "Content-Type=application/json" `
    --uri "https://graph.microsoft.com/v1.0/applications/$($aadApp.objectId)" `
    --body $appServiceJson }
Write-Host 'Done.'

Write-Status "Deploying the website..."
Invoke-Call { az webapp deployment source config-zip `
    --ids $websiteId `
    --src $websiteZipPath }

Write-Status "Deploying the workers..."
Invoke-Call { az functionapp deployment source config-zip `
    --ids @workerIds `
    --src $workerZipPath }
