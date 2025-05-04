$files = [ordered]@{
    "dotnet/aspnetcore"       = @{
        License  = "LICENSE.txt";
        Files    = @(
            "src/Shared/ThrowHelpers/ArgumentNullThrowHelper.cs",
            "src/Identity/Extensions.Core/src/Base32.cs"
        );
        Revision = "c096dbbbe652f03be926502d790eb499682eea13";
        Patches  = @(
            @{
                Description = "Add StringComparison.Ordinal to IndexOf"
                Path        = "0001-Add-StringComparison-to-IndexOf.patch"
            }
        )
    };
    "NuGet/NuGetGallery"      = @{
        License  = "LICENSE.txt"
        Files    = @(
            "src/Catalog/Helpers/Utils.cs",
            "src/NuGet.Services.Entities/IEntity.cs",
            "src/NuGet.Services.Entities/PackageFramework.cs",
            "src/NuGet.Services.Validation/Entities/EndCertificateStatus.cs",
            "src/NuGet.Services.Validation/Entities/EndCertificateUse.cs"
            "src/NuGetGallery.Core/Frameworks/FrameworkCompatibilityService.cs",
            "src/NuGetGallery.Core/Frameworks/FrameworkProductNames.cs",
            "src/NuGetGallery.Core/Frameworks/IPackageFrameworkCompatibilityFactory.cs",
            "src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibility.cs",
            "src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityBadges.cs",
            "src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityData.cs",
            "src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityFactory.cs",
            "src/NuGetGallery.Core/Frameworks/SupportedFrameworks.cs",
            "src/NuGetGallery.Core/Services/AssetFrameworkHelper.cs",
            "src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationException.cs",
            "src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationResult.cs",
            "src/Validation.PackageSigning.ValidateCertificate/ICertificateVerifier.cs",
            "src/Validation.PackageSigning.ValidateCertificate/OnlineCertificateVerifier.cs",
            "src/Validation.PackageSigning.ValidateCertificate/Primitives.cs"
        );
        Revision = "968432180cad66123c6541ba89b562e0cbb22a8d"
        Patches  = @(
            @{
                Description = "Remove unused property from ``PackageFramework`` and make ``FrameworkName`` settable"
                Path        = "0002-Remove-Package-make-FrameworkName-settable-in-Packag.patch"
            },
            @{
                Description = "Add ``ChainInfo`` property to ``CertificateVerificationResult`` to allow reading the chain before disposal"
                Path        = "0003-Add-type-parameter-to-CertificateVerificationResult-.patch"
            },
            @{
                Description = "Trim unneeded code from Utils.cs to allow just simple tag splitting"
                Path        = "0004-Remove-unused-code-from-Utils.cs.patch"
            },
            @{
                Description = "Remove unnecessary usings in Utils.cs"
                Path        = "0005-Remove-unnecessary-usings-in-Forks-Utils.cs.patch"
            }
        )
    };
    "Azure/azure-sdk-for-net" = @{
        License  = "LICENSE.txt"
        Files    = @(
            "sdk/core/Azure.Core/src/Pipeline/BearerTokenAuthenticationPolicy.cs",
            "sdk/core/Azure.Core/src/Shared/Argument.cs",
            "sdk/core/Azure.Core/src/Shared/AuthorizationChallengeParser.cs",
            "sdk/core/Azure.Core/src/Shared/TaskExtensions.cs"
        );
        Revision = "3288d385d85106150bb697d2b37e27f4cfd57d91";
        Patches  = @(
            @{
                Description = "Make AccessTokenCache reusable, use ILogger instead of Azure event source, cache AccessToken instead of HeaderValue, use StringComparison.Ordinal for string.Equals"
                Path        = "0006-Make-AccessTokenCache-reusable.patch"
            }
        )
    };
}

$encoding = New-Object System.Text.UTF8Encoding $true

$readme = "The following GitHub repositories were copied in part to this project."
$readme += [Environment]::NewLine

foreach ($pair in $files.GetEnumerator()) {
    $repository = $pair.Key
    $revision = $pair.Value.Revision
    Write-Host "Downloading files from $repository@$revision"

    # Download files
    foreach ($file in @($pair.Value.License) + $pair.Value.Files) {
        $url = "https://raw.githubusercontent.com/$repository/$revision/$file"
        $destPath = Join-Path $PSScriptRoot "$repository/$file"
        $dir = Split-Path $destPath -Parent

        if (!(Test-Path $dir)) {
            New-Item $dir -ItemType Directory | Out-Null
        }
        
        Write-Host "  Saving $file"
        $retryCount = 0
        $maxRetries = 5
        $success = $false

        while (-not $success -and $retryCount -lt $maxRetries) {
            try {
                $response = Invoke-WebRequest $url -UseBasicParsing

                if ($response.Headers["x-ratelimit-remaining"]) {
                    $rateLimitRemaining = $response.Headers["x-ratelimit-remaining"]
                    Write-Host "  x-ratelimit-remaining: $rateLimitRemaining"
                }

                $content = $response.Content.Replace("`r`n", "`n").Replace("`n", [Environment]::NewLine).TrimEnd()
                [IO.File]::WriteAllLines($destPath, $content, $encoding)
                $success = $true
            }
            catch [System.Net.WebException] {
                if ($_.Exception.Response.Headers["x-ratelimit-remaining"]) {
                    $rateLimitRemaining = $_.Exception.Response.Headers["x-ratelimit-remaining"]
                    Write-Host "  x-ratelimit-remaining: $rateLimitRemaining"
                }

                $retryCount++
                if ($_.Exception.Response -and ($_.Exception.Response.StatusCode -eq 429 -or $_.Exception.Response.StatusCode -eq 403)) {
                    $rateLimitReset = $_.Exception.Response.Headers["x-ratelimit-reset"]
                    $retryAfter = $_.Exception.Response.Headers["Retry-After"]

                    if ($rateLimitReset) {
                        $currentTime = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
                        $waitTime = [int]$rateLimitReset - $currentTime
                        if ($waitTime -gt 0) {
                            Write-Host "  Rate limit reached. Waiting until reset in $waitTime seconds..."
                            Start-Sleep -Seconds $waitTime
                        }
                    } elseif ($retryAfter) {
                        if ($retryAfter -as [int]) {
                            Write-Host "  Received Retry-After header as seconds. Retrying after $retryAfter seconds..."
                            Start-Sleep -Seconds ([int]$retryAfter)
                        } elseif ($retryAfter -as [string]) {
                            $retryAfterDate = [DateTimeOffset]::Parse($retryAfter)
                            $currentTime = [DateTimeOffset]::UtcNow
                            $waitTime = ($retryAfterDate - $currentTime).TotalSeconds
                            if ($waitTime -gt 0) {
                                Write-Host "  Received Retry-After header as date. Retrying after $waitTime seconds..."
                                Start-Sleep -Seconds ([int]$waitTime)
                            }
                        } else {
                            Write-Host "  Invalid Retry-After header format. Retrying after a default delay of 30 seconds..."
                            Start-Sleep -Seconds 30
                        }
                    } else {
                        Write-Host "  Rate limit reached. Retrying after a default delay of 30 seconds..."
                        Start-Sleep -Seconds 30
                    }
                } else {
                    throw
                }
            }
        }

        if (-not $success) {
            throw "Failed to download $file after $maxRetries attempts."
        }
    }

    # Apply patches
    foreach ($patch in $pair.Value.Patches) {
        Write-Host "  Applying $($patch.Path)"
        git apply (Join-Path $PSScriptRoot $patch.Path)
        if ($LASTEXITCODE -ne 0) {
            throw "git apply failed with exit code $LASTEXITCODE"
        }
    }

    # Append to the README
    $readme += [Environment]::NewLine
    $readme += "# [$repository](https://github.com/$repository)"
    $readme += [Environment]::NewLine
    $readme += [Environment]::NewLine
    $readme += "Copied license: [``$($pair.Value.License)``]($repository/$($pair.Value.License))"
    $readme += [Environment]::NewLine
    $readme += [Environment]::NewLine
    $readme += "Copied revision: [``$($pair.Value.Revision)``](https://github.com/$repository/tree/$($pair.Value.Revision))"
    $readme += [Environment]::NewLine
    $readme += [Environment]::NewLine
    $readme += "Files:"
    $readme += [Environment]::NewLine
    foreach ($file in $pair.Value.Files) {
        $readme += "  - [``$file``]($repository/$file)"
        $readme += [Environment]::NewLine
    }
    $readme += [Environment]::NewLine
    $readme += "Patches:"
    $readme += [Environment]::NewLine
    if ($pair.Value.Patches.Length -eq 0) {
        $readme += "  - (none)"
        $readme += [Environment]::NewLine
    }
    else {
        foreach ($patch in $pair.Value.Patches) {
            $readme += "  - [$($patch.Description)]($($patch.Path))"
            $readme += [Environment]::NewLine
        }
    }
}

Write-Host "Writing latest README.md"
$readmePath = Join-Path $PSScriptRoot "README.md"
[IO.File]::WriteAllLines($readmePath, $readme, $encoding)

Write-Host "Checking for uncommitted changes"
$changes = git status $PSScriptRoot --porcelain=v1 | Out-String
if ($changes) {
    if ($changes.Trim() -eq "M src/Forks/README.md") {
        $hint = [Environment]::NewLine + "The only file that changed is README.md. Try running src/Forks/downloads.ps1 and committing the changes."
    }
    else {
        $hint = ""
    }
    throw "There unexpected changes in the Fork project.$hint" + [Environment]::NewLine + $changes
}
else {
    Write-Host "No uncommitted changes found"
}