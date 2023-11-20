The following GitHub repositories were copied in part to this project.

# [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore)

Copied license: [`LICENSE.txt`](dotnet/aspnetcore/LICENSE.txt)

Copied revision: [`c096dbbbe652f03be926502d790eb499682eea13`](https://github.com/dotnet/aspnetcore/tree/c096dbbbe652f03be926502d790eb499682eea13)

Files:
  - [`src/Shared/ThrowHelpers/ArgumentNullThrowHelper.cs`](dotnet/aspnetcore/src/Shared/ThrowHelpers/ArgumentNullThrowHelper.cs)
  - [`src/Identity/Extensions.Core/src/Base32.cs`](dotnet/aspnetcore/src/Identity/Extensions.Core/src/Base32.cs)

Patches:
  - [Add StringComparison.Ordinal to IndexOf](0004-Add-StringComparison-to-IndexOf.patch)

# [NuGet/NuGet.Jobs](https://github.com/NuGet/NuGet.Jobs)

Copied license: [`LICENSE.txt`](NuGet/NuGet.Jobs/LICENSE.txt)

Copied revision: [`be3a837ea4add2d6376f16da562d67f83699cce0`](https://github.com/NuGet/NuGet.Jobs/tree/be3a837ea4add2d6376f16da562d67f83699cce0)

Files:
  - [`src/Catalog/Helpers/Utils.cs`](NuGet/NuGet.Jobs/src/Catalog/Helpers/Utils.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationException.cs`](NuGet/NuGet.Jobs/src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationException.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/Primitives.cs`](NuGet/NuGet.Jobs/src/Validation.PackageSigning.ValidateCertificate/Primitives.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationResult.cs`](NuGet/NuGet.Jobs/src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationResult.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/ICertificateVerifier.cs`](NuGet/NuGet.Jobs/src/Validation.PackageSigning.ValidateCertificate/ICertificateVerifier.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/OnlineCertificateVerifier.cs`](NuGet/NuGet.Jobs/src/Validation.PackageSigning.ValidateCertificate/OnlineCertificateVerifier.cs)

Patches:
  - [Trim unneeeded code from Utils.cs to allow just simple tag splitting](0001-Trim-unneeded-code-from-Utils.cs.patch)
  - [Add `ChainInfo` property to `CertificateVerificationResult` to allow reading the chain before disposal](0002-Add-chain-info-property-to-CertificateVerificationResult.patch)

# [NuGet/NuGetGallery](https://github.com/NuGet/NuGetGallery)

Copied license: [`LICENSE.txt`](NuGet/NuGetGallery/LICENSE.txt)

Copied revision: [`bc567a1a1e975d54dddc28b6f33af1fc3c76bca8`](https://github.com/NuGet/NuGetGallery/tree/bc567a1a1e975d54dddc28b6f33af1fc3c76bca8)

Files:
  - [`src/NuGet.Services.Entities/IEntity.cs`](NuGet/NuGetGallery/src/NuGet.Services.Entities/IEntity.cs)
  - [`src/NuGet.Services.Entities/PackageFramework.cs`](NuGet/NuGetGallery/src/NuGet.Services.Entities/PackageFramework.cs)
  - [`src/NuGetGallery.Core/Frameworks/FrameworkCompatibilityService.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/FrameworkCompatibilityService.cs)
  - [`src/NuGetGallery.Core/Frameworks/FrameworkProductNames.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/FrameworkProductNames.cs)
  - [`src/NuGetGallery.Core/Frameworks/IPackageFrameworkCompatibilityFactory.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/IPackageFrameworkCompatibilityFactory.cs)
  - [`src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibility.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibility.cs)
  - [`src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityBadges.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityBadges.cs)
  - [`src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityFactory.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityFactory.cs)
  - [`src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityTableData.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityTableData.cs)
  - [`src/NuGetGallery.Core/Frameworks/SupportedFrameworks.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/SupportedFrameworks.cs)
  - [`src/NuGetGallery.Core/Services/AssetFrameworkHelper.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Services/AssetFrameworkHelper.cs)

Patches:
  - [Remove unused property from `PackageFramework` and make `FrameworkName` settable](0003-Remove-unused-property-and-make-framework-name-setta.patch)
  - [Use NuGetFrameworkSorter.Instance to avoid obsolete warning](0005-Use-NuGetFrameworkSorter.Instance-to-avoid-obsolete-.patch)

# [NuGet/ServerCommon](https://github.com/NuGet/ServerCommon)

Copied license: [`License.md`](NuGet/ServerCommon/License.md)

Copied revision: [`dd614b153e2476b1bf1a9e8a7553a8625ed3cc87`](https://github.com/NuGet/ServerCommon/tree/dd614b153e2476b1bf1a9e8a7553a8625ed3cc87)

Files:
  - [`src/NuGet.Services.Validation/Entities/EndCertificateStatus.cs`](NuGet/ServerCommon/src/NuGet.Services.Validation/Entities/EndCertificateStatus.cs)
  - [`src/NuGet.Services.Validation/Entities/EndCertificateUse.cs`](NuGet/ServerCommon/src/NuGet.Services.Validation/Entities/EndCertificateUse.cs)

Patches:
  - (none)

