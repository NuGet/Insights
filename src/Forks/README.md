The following GitHub repositories were copied in part to this project.

# [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore)

Copied license: [`LICENSE.txt`](dotnet/aspnetcore/LICENSE.txt)

Copied revision: [`c096dbbbe652f03be926502d790eb499682eea13`](https://github.com/dotnet/aspnetcore/tree/c096dbbbe652f03be926502d790eb499682eea13)

Files:
  - [`src/Shared/ThrowHelpers/ArgumentNullThrowHelper.cs`](dotnet/aspnetcore/src/Shared/ThrowHelpers/ArgumentNullThrowHelper.cs)
  - [`src/Identity/Extensions.Core/src/Base32.cs`](dotnet/aspnetcore/src/Identity/Extensions.Core/src/Base32.cs)

Patches:
  - [Add StringComparison.Ordinal to IndexOf](0001-Add-StringComparison-to-IndexOf.patch)

# [NuGet/NuGetGallery](https://github.com/NuGet/NuGetGallery)

Copied license: [`LICENSE.txt`](NuGet/NuGetGallery/LICENSE.txt)

Copied revision: [`968432180cad66123c6541ba89b562e0cbb22a8d`](https://github.com/NuGet/NuGetGallery/tree/968432180cad66123c6541ba89b562e0cbb22a8d)

Files:
  - [`src/Catalog/Helpers/Utils.cs`](NuGet/NuGetGallery/src/Catalog/Helpers/Utils.cs)
  - [`src/NuGet.Services.Entities/IEntity.cs`](NuGet/NuGetGallery/src/NuGet.Services.Entities/IEntity.cs)
  - [`src/NuGet.Services.Entities/PackageFramework.cs`](NuGet/NuGetGallery/src/NuGet.Services.Entities/PackageFramework.cs)
  - [`src/NuGet.Services.Validation/Entities/EndCertificateStatus.cs`](NuGet/NuGetGallery/src/NuGet.Services.Validation/Entities/EndCertificateStatus.cs)
  - [`src/NuGet.Services.Validation/Entities/EndCertificateUse.cs`](NuGet/NuGetGallery/src/NuGet.Services.Validation/Entities/EndCertificateUse.cs)
  - [`src/NuGetGallery.Core/Frameworks/FrameworkCompatibilityService.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/FrameworkCompatibilityService.cs)
  - [`src/NuGetGallery.Core/Frameworks/FrameworkProductNames.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/FrameworkProductNames.cs)
  - [`src/NuGetGallery.Core/Frameworks/IPackageFrameworkCompatibilityFactory.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/IPackageFrameworkCompatibilityFactory.cs)
  - [`src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibility.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibility.cs)
  - [`src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityBadges.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityBadges.cs)
  - [`src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityData.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityData.cs)
  - [`src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityFactory.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/PackageFrameworkCompatibilityFactory.cs)
  - [`src/NuGetGallery.Core/Frameworks/SupportedFrameworks.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Frameworks/SupportedFrameworks.cs)
  - [`src/NuGetGallery.Core/Services/AssetFrameworkHelper.cs`](NuGet/NuGetGallery/src/NuGetGallery.Core/Services/AssetFrameworkHelper.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationException.cs`](NuGet/NuGetGallery/src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationException.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationResult.cs`](NuGet/NuGetGallery/src/Validation.PackageSigning.ValidateCertificate/CertificateVerificationResult.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/ICertificateVerifier.cs`](NuGet/NuGetGallery/src/Validation.PackageSigning.ValidateCertificate/ICertificateVerifier.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/OnlineCertificateVerifier.cs`](NuGet/NuGetGallery/src/Validation.PackageSigning.ValidateCertificate/OnlineCertificateVerifier.cs)
  - [`src/Validation.PackageSigning.ValidateCertificate/Primitives.cs`](NuGet/NuGetGallery/src/Validation.PackageSigning.ValidateCertificate/Primitives.cs)

Patches:
  - [Remove unused property from `PackageFramework` and make `FrameworkName` settable](0002-Remove-Package-make-FrameworkName-settable-in-Packag.patch)
  - [Add `ChainInfo` property to `CertificateVerificationResult` to allow reading the chain before disposal](0003-Add-type-parameter-to-CertificateVerificationResult-.patch)
  - [Trim unneeded code from Utils.cs to allow just simple tag splitting](0004-Remove-unused-code-from-Utils.cs.patch)

