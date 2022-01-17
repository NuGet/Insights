$files = [ordered]@{
    "dotnet/aspnetcore"  = @{
        License  = "LICENSE.txt";
        Files    = @(
            "src/Identity/Extensions.Core/src/Base32.cs"
        );
        Revision = "4931b1929188349b438575803bcec889a9a7d190";
        Patches  = @()
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
    }
}

$readme = "The following GitHub repositories were copied in part to this project."
$readme += "`r`n"

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
        $response = Invoke-WebRequest $url
        $content = $response.Content.Replace("`r`n", "`n").Replace("`n", "`r`n").TrimEnd()
        $content | Out-File $destPath -Encoding UTF8
    }

    # Apply patches
    foreach ($patch in $pair.Value.Patches) {
        Write-Host "  Applying $($patch.Path)"
        git apply (Join-Path $PSScriptRoot $patch.Path)
    }

    # Append to the README
    $readme += "`r`n"
    $readme += "# [$repository](https://github.com/$repository)"
    $readme += "`r`n`r`n"
    $readme += "Copied license: [``$($pair.Value.License)``]($repository/$($pair.Value.License))"
    $readme += "`r`n`r`n"
    $readme += "Copied revision: [``$($pair.Value.Revision)``](https://github.com/$repository/tree/$($pair.Value.Revision))"
    $readme += "`r`n`r`n"
    $readme += "Files:"
    $readme += "`r`n"
    foreach ($file in $pair.Value.Files) {
        $readme += "  - [``$file``]($repository/$file)"
        $readme += "`r`n"
    }
    $readme += "`r`n"
    $readme += "Patches:"
    $readme += "`r`n"
    if ($pair.Value.Patches.Length -eq 0) {
        $readme += "  - (none)"
        $readme += "`r`n"
    }
    else {
        foreach ($patch in $pair.Value.Patches) {
            $readme += "  - [$($patch.Description)]($($patch.Path))"
        }
    }
}

Write-Host "Writing latest README.md"
$readme | Out-File (Join-Path $PSScriptRoot "README.md") -Encoding UTF8

Write-Host "Checking for uncommitted changes"
$changes = git status $PSScriptRoot --porcelain=v1 | Out-String
if ($changes) {
    if ($changes.Trim() -eq "M src/Forks/README.md") {
        $hint = "`r`nThe only file that changed is README.md. Try running src/Forks/downloads.ps1 and committing the changes."
    } else {
        $hint = ""
    }
    throw "There unexpected changes in the Fork project.$hint`r`n$changes"
} else {
    Write-Host "No uncommitted changes found"
}
