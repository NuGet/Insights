// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public enum CatalogScanDriverType
    {
        /// <summary>
        /// Implemented by <see cref="FindLatestCatalogLeafScan.LatestCatalogLeafScanStorage"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This is a helper catalog scan used by <see cref="CatalogIndexScanMessageProcessor"/> when the driver returns
        /// <see cref="CatalogIndexScanResult.ExpandLatestLeaves"/>. This allows another driver to only process the latest
        /// catalog leaf per version instead of duplicating effort which is inevitable in the NuGet.org catalog.
        /// </summary>
        Internal_FindLatestCatalogLeafScan,

        /// <summary>
        /// Implemented by <see cref="FindLatestCatalogLeafScanPerId.LatestCatalogLeafScanPerIdStorage"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This is a helper catalog scan used by <see cref="CatalogIndexScanMessageProcessor"/> when the driver returns
        /// <see cref="CatalogIndexScanResult.ExpandLatestLeavesPerId"/>. This allows another driver to only process the latest
        /// catalog leaf per ID instead of duplicating effort which is inevitable in the NuGet.org catalog.
        /// </summary>
        Internal_FindLatestCatalogLeafScanPerId,

        /// <summary>
        /// Implemented by <see cref="LoadBucketedPackage.BucketedPackageStorage"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This driver records the latest catalog leaf URL for each package version to Azure Table Storage partitions them
        /// into 1000 buckets.
        /// </summary>
        LoadBucketedPackage,

        /// <summary>
        /// Implemented by <see cref="LoadPackageArchive.LoadPackageArchiveDriver"/>. Downloads interesting parts of the .nupkg
        /// file stored in the V3 flat container and stores the data in Azure Table Storage for other drivers to use.
        /// </summary>
        LoadPackageArchive,

        /// <summary>
        /// Implemented by <see cref="LoadSymbolPackageArchive.LoadSymbolPackageArchiveDriver"/>. Downloads interesting parts of the .snupkg
        /// file stored in the symbol packages container and stores the data in Azure Table Storage for other drivers to use.
        /// </summary>
        LoadSymbolPackageArchive,

        /// <summary>
        /// Implemented by <see cref="LoadPackageManifest.LoadPackageManifestDriver"/>. Downloads the .nuspec from the
        /// V3 flat container and stores it in Azure Table Storage for other drivers to use.
        /// </summary>
        LoadPackageManifest,

        /// <summary>
        /// Implemented by <see cref="LoadPackageReadme.LoadPackageReadmeDriver"/>. Downloads the README from the
        /// V3 flat container and stores it in Azure Table Storage for other drivers to use. If configured, it also
        /// attempts to download the legacy README from another storage location if the README is not embedded.
        /// </summary>
        LoadPackageReadme,

        /// <summary>
        /// Implemented by <see cref="LoadPackageVersion.LoadPackageVersionDriver"/>. Determines the deleted, listed,
        /// and SemVer status for every package version and stores it in Azure Table Storage for other drivers to use.
        /// </summary>
        LoadPackageVersion,

        /// <summary>
        /// Implemented by <see cref="LoadLatestPackageLeaf.LatestPackageLeafStorage"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This driver records the latest catalog leaf URL for each package version to Azure Table Storage.
        /// </summary>
        LoadLatestPackageLeaf,

        /// <summary>
        /// Implemented by <see cref="BuildVersionSet.BuildVersionSetDriver"/>. Builds a compact data structure that
        /// can be loaded in memory to quickly determine if a package ID and version exists on NuGet.org.
        /// </summary>
        BuildVersionSet,

        /// <summary>
        /// Implemented by <see cref="PackageArchiveToCsv.PackageArchiveToCsvDriver"/>.
        /// Extracts metadata about each entry in a package's ZIP archive (the .nupkg file).
        /// </summary>
        PackageArchiveToCsv,

        /// <summary>
        /// Implemented by <see cref="SymbolPackageArchiveToCsv.SymbolPackageArchiveToCsvDriver"/>.
        /// Extracts metadata about each entry in a symbol package's ZIP archive (the .snupkg file).
        /// </summary>
        SymbolPackageArchiveToCsv,

        /// <summary>
        /// Implemented by <see cref="PackageAssemblyToCsv.PackageAssemblyToCsvDriver"/>. For packages that contain
        /// assemblies, downloads the entire .nupkg and extract metadata about each assembly.
        /// </summary>
        PackageAssemblyToCsv,

        /// <summary>
        /// Implemented by <see cref="PackageAssetToCsv.PackageAssetToCsvDriver"/>. Runs NuGet restore pattern sets against
        /// all files in each packages. This essentially determines all package assets the NuGet PackageReference restore
        /// recognizes.
        /// </summary>
        PackageAssetToCsv,

#if ENABLE_CRYPTOAPI
        /// <summary>
        /// Implemented by <see cref="PackageCertificateToCsv.PackageCertificateToCsvDriver"/>. Loads all certificate
        /// chain information from the package signature into storage and tracks the many-to-many relationship between
        /// packages and certificates.
        /// </summary>
        PackageCertificateToCsv,
#endif

        /// <summary>
        /// Implemented by <see cref="PackageSignatureToCsv.PackageSignatureToCsvDriver"/>. Extracts information from each
        /// NuGet package signature, determining things like certificate issuers and whether the package is author signed.
        /// </summary>
        PackageSignatureToCsv,

        /// <summary>
        /// Implemented by <see cref="PackageManifestToCsv.PackageManifestToCsvDriver"/>. This driver reads all package
        /// manifests (a.k.a. .nuspec files) and dumps the metadata to CSV. This implementation uses NuGet's
        /// <see cref="Packaging.NuspecReader"/> to interpret the data as the NuGet client would.
        /// </summary>
        PackageManifestToCsv,

        /// <summary>
        /// Implemented by <see cref="PackageReadmeToCsv.PackageReadmeToCsvDriver"/>. This driver reads all package
        /// readmes and dumps the content and some metadata to CSV.
        /// </summary>
        PackageReadmeToCsv,

        /// <summary>
        /// Implemented by <see cref="PackageLicenseToCsv.PackageLicenseToCsvDriver"/>. This driver reads license
        /// expressions and license files and dumps the content and some metadata to CSV.
        /// </summary>
        PackageLicenseToCsv,

        /// <summary>
        /// Implemented by <see cref="PackageVersionToCsv.PackageVersionToCsvDriver"/>. This driver determines the
        /// latest version per package ID by using the data set up by <see cref="LoadPackageVersion"/>.
        /// </summary>
        PackageVersionToCsv,

#if ENABLE_NPE
        /// <summary>
        /// Implemented by <see cref="NuGetPackageExplorerToCsv.NuGetPackageExplorerToCsvDriver"/>. This driver runs
        /// NuGet Package Explorer (NPE) assembly and symbol verification logic.
        /// </summary>
        NuGetPackageExplorerToCsv,
#endif

        /// <summary>
        /// Implemented by <see cref="PackageCompatibilityToCsv.PackageCompatibilityToCsvDriver"/>. This driver uses
        /// various algorithms to determine what frameworks/platforms this package is compatible with.
        /// </summary>
        PackageCompatibilityToCsv,

        /// <summary>
        /// Implemented by <see cref="PackageIconToCsv.PackageIconToCsvDriver"/>. This driver analyzes the package icon
        /// that is available on flat container. This is either the embedded icon or the downloaded icon from the icon
        /// URL (legacy).
        /// </summary>
        PackageIconToCsv,

        /// <summary>
        /// Implemented by <see cref="CatalogDataToCsv.CatalogDataToCsvDriver"/>. This driver reads catalog leaves for
        /// metadata found there, e.g. deprecation and vulnerability metadata.
        /// </summary>
        CatalogDataToCsv,

        /// <summary>
        /// Implemented by <see cref="PackageContentToCsv.PackageContentToCsvDriver"/>. This driver loads package
        /// content with specific file extensions much like <see cref="PackageReadmeToCsv.PackageReadmeToCsvDriver"/>.
        /// </summary>
        PackageContentToCsv,
    }
}
