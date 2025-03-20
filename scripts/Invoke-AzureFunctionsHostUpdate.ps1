[CmdletBinding()]
param (  
    [Parameter(Mandatory = $false)]
    [string]$RemoteName = "origin",

    [Parameter(Mandatory = $false)]
    [string]$GitAuthorizationHeader,

    [Parameter(Mandatory = $false)]
    [string]$PullRequestAuthorizationHeader,
    
    [Parameter(Mandatory = $false)]
    [string]$PersonalAccessToken
)

$changes = git status --porcelain=v1 2>&1 | Out-String
if (($LASTEXITCODE -ne 0) -or ($changes.Trim() -ne "")) {
    throw "There are pending changes in the working directory. This script must be run on a clean working directory."
}

if ($PersonalAccessToken) {
    $personalAccessTokenAuthorization = "basic $([Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes(":$bearerToken")))"
}

$hostVersionPath = (Join-Path $PSScriptRoot "../deploy/host-version.ps1")
. $hostVersionPath
Write-Host "Current host version: $AzureFunctionsHostVersion"

$githubBaseUrl = "https://api.github.com/repos/azure/azure-functions-host";

$currentRelease = Invoke-RestMethod "$githubBaseUrl/releases/tags/$AzureFunctionsHostVersion"
if ($currentRelease.tag_name -ne $AzureFunctionsHostVersion) {
    throw "Current release not found. Fetched tag_name: $($currentRelease.tag_name)"
}

Write-Host "Current release published at: $($currentRelease.published_at)"
Write-Host

# "v4.10xx.*" versions
# See https://github.com/Azure/azure-functions-host/issues/10374
$tagPattern = "v4\.10\d\d\.\d+$"

$releases = Invoke-RestMethod "$githubBaseUrl/releases?per_page=100"
$candidates = @()
foreach ($release in $releases) {
    if ($release.tag_name -eq $AzureFunctionsHostVersion) {
        Write-Host "Skipping release (same version): $($release.tag_name)" -ForegroundColor DarkGray
        continue
    }

    if ($release.published_at -le $currentRelease.published_at) {
        Write-Host "Skipping release (older): $($release.tag_name)" -ForegroundColor DarkGray
        continue
    }

    if ($release.draft) {
        Write-Host "Skipping release (draft): $($release.tag_name)" -ForegroundColor Yellow
        continue
    }

    if ($release.prerelease) {
        Write-Host "Skipping release (prerelease): $($release.tag_name)" -ForegroundColor Yellow
        continue
    }

    if ($release.tag_name -notmatch $tagPattern) {
        Write-Host "Skipping release (wrong tag pattern): $($release.tag_name)" -ForegroundColor DarkGray
        continue
    }
    
    Write-Host "Found newer release: $($release.tag_name)"
    $candidates += $release
}

if ($candidates.Count -eq 0) {
    Write-Host "No newer release found." -ForegroundColor Yellow
    exit
}

$latest = $candidates | Sort-Object -Property published_at -Descending | Select-Object -First 1
Write-Host "Selecting release: $($latest.tag_name)" -ForegroundColor Green

Write-Host

Write-Host "Checking for existing git branch using the new host version"
$gitAuthArgs = @()
if ($GitAuthorizationHeader) {
    Write-Host "Using provided bearer token for git push authorization."
    $gitAuthArgs += @("-c", "http.extraHeader=`"Authorization: $GitAuthorizationHeader`"")
}
elseif ($PersonalAccessToken) {
    Write-Host "Using provided personal access token for git push authorization."
    $gitAuthArgs += @("-c", "http.extraHeader=`"Authorization: bearer $PersonalAccessToken`"")
}
git @gitAuthArgs fetch $RemoteName
if ($LASTEXITCODE -ne 0) { throw "Failed to fetch the remote branches." }
$branchNamePrefix = "automation/update-host/$($latest.tag_name)/"
$remoteBranches = git branch -r 
$branchName = $remoteBranches | ForEach-Object { $_.Trim() } | Where-Object { $_.StartsWith("$RemoteName/$branchNamePrefix") }
$message = "Update Azure Functions Host version to $($latest.tag_name)"
if ($branchName) {
    Write-Host "Branch already exists. Skipped branch creation."
    $branchName = $branchName.Substring($RemoteName.Length + 1)
}
else {    
    $currentBranch = git rev-parse --abbrev-ref HEAD
    $branchName = "$branchNamePrefix$(New-Guid)"
    try {
        Write-Host "Creating a new branch $branchName for the update"
        git checkout -B $branchName
        if ($LASTEXITCODE -ne 0) { throw "Failed to create a new branch." }

        Write-Host "Updating Azure Functions Host version to $($latest.tag_name)"
        "`$AzureFunctionsHostVersion = `"$($latest.tag_name)`"" | Out-File $hostVersionPath -Encoding utf8

        Write-Host "Committing the change"
        git commit -am $message
        if ($LASTEXITCODE -ne 0) { throw "Failed to commit the change." }
        
        Write-Host "Pushing the branch to the remote"        
        git @gitAuthArgs push $RemoteName $branchName
        if ($LASTEXITCODE -ne 0) { throw "Failed to push the branch." }
    }
    finally {
        git checkout $currentBranch
    }
}

Write-Host "Using branch name: $branchName" -ForegroundColor Green
Write-Host ""

Write-Host "Creating a pull request"
$pullRequestUrl = "https://dev.azure.com/devdiv/DevDiv/_apis/git/repositories/NuGet-Insights/pullrequests?api-version=7.1"

$pullRequestBody = @{
    sourceRefName = "refs/heads/$branchName"
    targetRefName = "refs/heads/main"
    title         = $message
    description   = "This is an automated PR to update the Azure Functions Host version to $($latest.tag_name)."
} | ConvertTo-Json -Depth 10

$pullRequestHeaders = @{
    "Content-Type" = "application/json"
}

if ($PullRequestAuthorizationHeader) {
    $pullRequestHeaders["Authorization"] = $PullRequestAuthorizationHeader
}
elseif ($personalAccessTokenAuthorization) {
    $pullRequestHeaders["Authorization"] = $personalAccessTokenAuthorization
}

try {
    $response = Invoke-WebRequest -Method POST -Uri $pullRequestUrl -Headers $pullRequestHeaders -Body $pullRequestBody
    if ($response.StatusCode -ne 201) {
        throw "Failed to create a pull request. Status code: $($response.StatusCode)"
    }
    $responseJson = $response | ConvertFrom-Json
    Write-Host "Pull request created successfully with ID: $($responseJson.pullRequestId)"
    $pullRequestWebUrl = "https://dev.azure.com/devdiv/DevDiv/_git/NuGet-Insights/pullrequest/$($responseJson.pullRequestId)"
    Write-Host "Pull request web URL: $pullRequestWebUrl" -ForegroundColor Green
}
catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "A pull request already exists for this Azure Functions Host version." -ForegroundColor Yellow
        Write-Host "$_" -ForegroundColor Yellow
    }
    else {
        throw
    }
}