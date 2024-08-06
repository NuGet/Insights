$files = [ordered]@{
    "dotnet/aspnetcore"  = @{
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
    "NuGet/NuGetGallery" = @{
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
    Write-Host "Downloads files from $repository@$revision"

    # Download files
    foreach ($file in @($pair.Value.License) + $pair.Value.Files) {
        $url = "https://raw.githubusercontent.com/$repository/$revision/$file"
        $destPath = Join-Path $PSScriptRoot "$repository/$file"
        $dir = Split-Path $destPath -Parent

        if (!(Test-Path $dir)) {
            New-Item $dir -ItemType Directory | Out-Null
        }
        
        Write-Host "  Saving $file"
        $response = Invoke-WebRequest $url -UseBasicParsing
        $content = $response.Content.Replace("`r`n", "`n").Replace("`n", [Environment]::NewLine).TrimEnd()
        [IO.File]::WriteAllLines($destPath, $content, $encoding)
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
