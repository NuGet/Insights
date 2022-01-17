$files = [ordered]@{
    "dotnet/aspnetcore"  = @{
        License  = "LICENSE.txt";
        Files    = @(
            "src/Identity/Extensions.Core/src/Base32.cs"
        );
        Revision = "4931b1929188349b438575803bcec889a9a7d190";
        Patches  = @()
    };
    "NuGet/NuGet.Jobs"   = @{
        License  = "LICENSE.txt"
        Files    = @(
            "src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationException.cs",
            "src/Validation.PackageSigning.ValidateCertificate/Primitives.cs",
            "src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationResult.cs",
            "src/Validation.PackageSigning.ValidateCertificate/ICertificateVerifier.cs",
            "src/Validation.PackageSigning.ValidateCertificate/OnlineCertificateVerifier.cs"
        );
        Revision = "be3a837ea4add2d6376f16da562d67f83699cce0"
        Patches  = @(
            @{
                Description = "Add ``ChainInfo`` property to ``CertificateVerificationResult`` to allow reading the chain before disposal"
                Path        = "0002-Add-chain-info-property-to-CertificateVerificationResult.patch"
            }
        )
    };
    "NuGet/NuGetGallery" = @{
        License  = "LICENSE.txt"
        Files    = @(
            "src/NuGetGallery.Services/PackageManagement/PackageService.cs"
        );
        Revision = "6df282d39c845f45ebc7fa131ad7a53a6952da00"
        Patches  = @(
            @{
                Description = "Remove unused methods from ``PackageService``"
                Path        = "0001-Remove-unused-methods-from-PackageService.patch"
            }
        )
    };
    "NuGet/ServerCommon" = @{
        License  = "License.md";
        Files    = @(
            "src/NuGet.Services.Validation/Entities/EndCertificateStatus.cs",
            "src/NuGet.Services.Validation/Entities/EndCertificateUse.cs"
        );
        Revision = "dd614b153e2476b1bf1a9e8a7553a8625ed3cc87";
        Patches  = @()
    }
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
    } else {
        $hint = ""
    }
    throw "There unexpected changes in the Fork project.$hint" + [Environment]::NewLine + $changes
} else {
    Write-Host "No uncommitted changes found"
}
