// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial struct CatalogScanDriverType
    {
        /// <summary>
        /// Implemented by <see cref="LoadBucketedPackage.BucketedPackageStorageFactory"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This driver records the latest catalog leaf URL for each package version to Azure Table Storage partitions them
        /// into 1000 buckets.
        /// </summary>
        public static CatalogScanDriverType LoadBucketedPackage { get; } = new CatalogScanDriverType(nameof(LoadBucketedPackage));

        /// <summary>
        /// Implemented by <see cref="LoadPackageArchive.LoadPackageArchiveDriver"/>. Downloads interesting parts of the .nupkg
        /// file stored in the V3 flat container and stores the data in Azure Table Storage for other drivers to use.
        /// </summary>
        public static CatalogScanDriverType LoadPackageArchive { get; } = new CatalogScanDriverType(nameof(LoadPackageArchive));

        /// <summary>
        /// Implemented by <see cref="LoadSymbolPackageArchive.LoadSymbolPackageArchiveDriver"/>. Downloads interesting parts of the .snupkg
        /// file stored in the symbol packages container and stores the data in Azure Table Storage for other drivers to use.
        /// </summary>
        public static CatalogScanDriverType LoadSymbolPackageArchive { get; } = new CatalogScanDriverType(nameof(LoadSymbolPackageArchive));

        /// <summary>
        /// Implemented by <see cref="LoadPackageManifest.LoadPackageManifestDriver"/>. Downloads the .nuspec from the
        /// V3 flat container and stores it in Azure Table Storage for other drivers to use.
        /// </summary>
        public static CatalogScanDriverType LoadPackageManifest { get; } = new CatalogScanDriverType(nameof(LoadPackageManifest));

        /// <summary>
        /// Implemented by <see cref="LoadPackageReadme.LoadPackageReadmeDriver"/>. Downloads the README from the
        /// V3 flat container and stores it in Azure Table Storage for other drivers to use. If configured, it also
        /// attempts to download the legacy README from another storage location if the README is not embedded.
        /// </summary>
        public static CatalogScanDriverType LoadPackageReadme { get; } = new CatalogScanDriverType(nameof(LoadPackageReadme));

        /// <summary>
        /// Implemented by <see cref="LoadPackageVersion.LoadPackageVersionDriver"/>. Determines the deleted, listed = nameof(listed);
        /// and SemVer status for every package version and stores it in Azure Table Storage for other drivers to use.
        /// </summary>
        public static CatalogScanDriverType LoadPackageVersion { get; } = new CatalogScanDriverType(nameof(LoadPackageVersion));

        /// <summary>
        /// Implemented by <see cref="LoadLatestPackageLeaf.LatestPackageLeafStorageFactory"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This driver records the latest catalog leaf URL for each package version to Azure Table Storage.
        /// </summary>
        public static CatalogScanDriverType LoadLatestPackageLeaf { get; } = new CatalogScanDriverType(nameof(LoadLatestPackageLeaf));

        /// <summary>
        /// Implemented by <see cref="BuildVersionSet.BuildVersionSetDriver"/>. Builds a compact data structure that
        /// can be loaded in memory to quickly determine if a package ID and version exists on NuGet.org.
        /// </summary>
        public static CatalogScanDriverType BuildVersionSet { get; } = new CatalogScanDriverType(nameof(BuildVersionSet));

        /// <summary>
        /// Implemented by <see cref="PackageArchiveToCsv.PackageArchiveToCsvDriver"/>.
        /// Extracts metadata about each entry in a package's ZIP archive (the .nupkg file).
        /// </summary>
        public static CatalogScanDriverType PackageArchiveToCsv { get; } = new CatalogScanDriverType(nameof(PackageArchiveToCsv));

        /// <summary>
        /// Implemented by <see cref="SymbolPackageArchiveToCsv.SymbolPackageArchiveToCsvDriver"/>.
        /// Extracts metadata about each entry in a symbol package's ZIP archive (the .snupkg file).
        /// </summary>
        public static CatalogScanDriverType SymbolPackageArchiveToCsv { get; } = new CatalogScanDriverType(nameof(SymbolPackageArchiveToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageFileToCsv.PackageFileToCsvDriver"/>.
        /// Gathers common information (e.g. hash) about each ZIP file in the package (the .nupkg file).
        /// </summary>
        public static CatalogScanDriverType PackageFileToCsv { get; } = new CatalogScanDriverType(nameof(PackageFileToCsv));

        /// <summary>
        /// Implemented by <see cref="SymbolPackageFileToCsv.SymbolPackageFileToCsvDriver"/>.
        /// Gathers common information (e.g. hash) about each ZIP file in the symbol package (the .snupkg file).
        /// </summary>
        public static CatalogScanDriverType SymbolPackageFileToCsv { get; } = new CatalogScanDriverType(nameof(SymbolPackageFileToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageAssemblyToCsv.PackageAssemblyToCsvDriver"/>. For packages that contain
        /// assemblies, downloads the entire .nupkg and extract metadata about each assembly.
        /// </summary>
        public static CatalogScanDriverType PackageAssemblyToCsv { get; } = new CatalogScanDriverType(nameof(PackageAssemblyToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageAssetToCsv.PackageAssetToCsvDriver"/>. Runs NuGet restore pattern sets against
        /// all files in each packages. This essentially determines all package assets the NuGet PackageReference restore
        /// recognizes.
        /// </summary>
        public static CatalogScanDriverType PackageAssetToCsv { get; } = new CatalogScanDriverType(nameof(PackageAssetToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageSignatureToCsv.PackageSignatureToCsvDriver"/>. Extracts information from each
        /// NuGet package signature, determining things like certificate issuers and whether the package is author signed.
        /// </summary>
        public static CatalogScanDriverType PackageSignatureToCsv { get; } = new CatalogScanDriverType(nameof(PackageSignatureToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageManifestToCsv.PackageManifestToCsvDriver"/>. This driver reads all package
        /// manifests (a.k.a. .nuspec files) and dumps the metadata to CSV. This implementation uses NuGet's
        /// <see cref="Packaging.NuspecReader"/> to interpret the data as the NuGet client would.
        /// </summary>
        public static CatalogScanDriverType PackageManifestToCsv { get; } = new CatalogScanDriverType(nameof(PackageManifestToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageReadmeToCsv.PackageReadmeToCsvDriver"/>. This driver reads all package
        /// readmes and dumps the content and some metadata to CSV.
        /// </summary>
        public static CatalogScanDriverType PackageReadmeToCsv { get; } = new CatalogScanDriverType(nameof(PackageReadmeToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageLicenseToCsv.PackageLicenseToCsvDriver"/>. This driver reads license
        /// expressions and license files and dumps the content and some metadata to CSV.
        /// </summary>
        public static CatalogScanDriverType PackageLicenseToCsv { get; } = new CatalogScanDriverType(nameof(PackageLicenseToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageVersionToCsv.PackageVersionToCsvDriver"/>. This driver determines the
        /// latest version per package ID by using the data set up by <see cref="LoadPackageVersion"/>.
        /// </summary>
        public static CatalogScanDriverType PackageVersionToCsv { get; } = new CatalogScanDriverType(nameof(PackageVersionToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageCompatibilityToCsv.PackageCompatibilityToCsvDriver"/>. This driver uses
        /// various algorithms to determine what frameworks/platforms this package is compatible with.
        /// </summary>
        public static CatalogScanDriverType PackageCompatibilityToCsv { get; } = new CatalogScanDriverType(nameof(PackageCompatibilityToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageIconToCsv.PackageIconToCsvDriver"/>. This driver analyzes the package icon
        /// that is available on flat container. This is either the embedded icon or the downloaded icon from the icon
        /// URL (legacy).
        /// </summary>
        public static CatalogScanDriverType PackageIconToCsv { get; } = new CatalogScanDriverType(nameof(PackageIconToCsv));

        /// <summary>
        /// Implemented by <see cref="CatalogDataToCsv.CatalogDataToCsvDriver"/>. This driver reads catalog leaves for
        /// metadata found there, e.g. deprecation and vulnerability metadata.
        /// </summary>
        public static CatalogScanDriverType CatalogDataToCsv { get; } = new CatalogScanDriverType(nameof(CatalogDataToCsv));

        /// <summary>
        /// Implemented by <see cref="PackageContentToCsv.PackageContentToCsvDriver"/>. This driver loads package
        /// content with specific file extensions much like <see cref="PackageReadmeToCsv.PackageReadmeToCsvDriver"/>.
        /// </summary>
        public static CatalogScanDriverType PackageContentToCsv { get; } = new CatalogScanDriverType(nameof(PackageContentToCsv));
    }
}
