// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.BuildVersionSet;
using NuGet.Insights.Worker.CatalogDataToCsv;
using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.LoadLatestPackageLeaf;
using NuGet.Insights.Worker.LoadPackageArchive;
using NuGet.Insights.Worker.LoadPackageManifest;
using NuGet.Insights.Worker.LoadPackageReadme;
using NuGet.Insights.Worker.LoadPackageVersion;
using NuGet.Insights.Worker.LoadSymbolPackageArchive;
using NuGet.Insights.Worker.PackageArchiveToCsv;
using NuGet.Insights.Worker.PackageAssemblyToCsv;
using NuGet.Insights.Worker.PackageAssetToCsv;
using NuGet.Insights.Worker.PackageCompatibilityToCsv;
using NuGet.Insights.Worker.PackageContentToCsv;
using NuGet.Insights.Worker.PackageFileToCsv;
using NuGet.Insights.Worker.PackageIconToCsv;
using NuGet.Insights.Worker.PackageLicenseToCsv;
using NuGet.Insights.Worker.PackageManifestToCsv;
using NuGet.Insights.Worker.PackageReadmeToCsv;
using NuGet.Insights.Worker.PackageSignatureToCsv;
using NuGet.Insights.Worker.PackageVersionToCsv;
using NuGet.Insights.Worker.SymbolPackageArchiveToCsv;
using NuGet.Insights.Worker.SymbolPackageFileToCsv;

#nullable enable

namespace NuGet.Insights.Worker
{
    public static partial class CatalogScanDriverMetadata
    {
        private partial record DriverMetadata
        {
            // only needs catalog pages, not leaves
            public static DriverMetadata BuildVersionSet =>
                OnlyCatalogRange<BuildVersionSetDriver>(CatalogScanDriverType.BuildVersionSet);

            // needs all catalog leaves
            public static DriverMetadata CatalogDataToCsv =>
                Csv<PackageDeprecationRecord, PackageVulnerabilityRecord, CatalogLeafItemRecord>(CatalogScanDriverType.CatalogDataToCsv) with
                {
                    DefaultMin = CatalogClient.NuGetOrgMin,
                    OnlyLatestLeavesSupport = false,
                    BucketRangeSupport = false,
                    GetBucketKey = null,
                };

            // uses find latest driver, only reads catalog pages
            public static DriverMetadata LoadBucketedPackage =>
                OnlyCatalogRange<FindLatestLeafDriver<BucketedPackage>>(CatalogScanDriverType.LoadBucketedPackage);

            // uses find latest driver, only reads catalog pages
            public static DriverMetadata LoadLatestPackageLeaf =>
                OnlyCatalogRange<FindLatestLeafDriver<LatestPackageLeaf>>(CatalogScanDriverType.LoadLatestPackageLeaf);

            public static DriverMetadata LoadPackageArchive =>
                Default<LoadPackageArchiveDriver>(CatalogScanDriverType.LoadPackageArchive) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.Nupkg,
                };

            public static DriverMetadata LoadPackageManifest =>
                Default<LoadPackageManifestDriver>(CatalogScanDriverType.LoadPackageManifest) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.Nuspec,
                };

            public static DriverMetadata LoadPackageReadme =>
                Default<LoadPackageReadmeDriver>(CatalogScanDriverType.LoadPackageReadme) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.Readme,

                    // README is not always embedded in the package and can be updated without a catalog update.
                    UpdatedOutsideOfCatalog = true,
                };

            // internally uses find latest driver
            public static DriverMetadata LoadPackageVersion =>
                Default<LoadPackageVersionDriver>(CatalogScanDriverType.LoadPackageVersion) with
                {
                    OnlyLatestLeavesSupport = false,
                };

            public static DriverMetadata LoadSymbolPackageArchive =>
                Default<LoadSymbolPackageArchiveDriver>(CatalogScanDriverType.LoadSymbolPackageArchive) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.Snupkg,

                    // Symbol package files (.snupkg) can be replaced and removed without a catalog update.
                    UpdatedOutsideOfCatalog = true,
                };

            public static DriverMetadata PackageArchiveToCsv =>
                Csv<PackageArchiveRecord, PackageArchiveEntry>(CatalogScanDriverType.PackageArchiveToCsv) with
                {
                    Dependencies = [CatalogScanDriverType.PackageFileToCsv],
                };

            public static DriverMetadata PackageAssemblyToCsv =>
                Csv<PackageAssembly>(CatalogScanDriverType.PackageAssemblyToCsv) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.Nupkg,
                    Dependencies = [CatalogScanDriverType.LoadPackageArchive],
                    LeafScanBatchSize = 10,
                };

            public static DriverMetadata PackageAssetToCsv =>
                Csv<PackageAsset>(CatalogScanDriverType.PackageAssetToCsv) with
                {
                    // Similar to PackageCompatibilityToCsv, an unsupported framework (or an existing framework) can become
                    // supported or change its interpretion over time. This is pretty unlikely so we don't reprocess
                    // this driver.
                    UpdatedOutsideOfCatalog = false,
                    Dependencies = [CatalogScanDriverType.LoadPackageArchive],
                };

            public static DriverMetadata PackageCompatibilityToCsv =>
                Csv<PackageCompatibility>(CatalogScanDriverType.PackageCompatibilityToCsv) with
                {
                    // This driver uses a compatibility map baked into NuGet/NuGetGallery which uses the NuGet.Frameworks
                    // package for framework compatibility. We could choose to periodically reprocess package compatibility
                    // so that changes in TFM mapping and computed frameworks automatically get picked up. For now, we won't
                    // do that and force a manual "Reset" operation from the admin panel to recompute all compatibilities.
                    // The main part of this data that changes is computed compatibility. Newly supported frameworks can
                    // also lead to changes in results, but this would take a package owner guessing or colliding with this
                    // new framework in advance, leading to an "unsupported" framework reparsing as a supported framework.
                    UpdatedOutsideOfCatalog = false,
                    Dependencies = [CatalogScanDriverType.LoadPackageArchive, CatalogScanDriverType.LoadPackageManifest],
                };

            public static DriverMetadata PackageContentToCsv =>
                Csv<PackageContent>(CatalogScanDriverType.PackageContentToCsv) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.Nupkg,
                    Dependencies = [CatalogScanDriverType.LoadPackageArchive],
                    LeafScanBatchSize = 10,
                };

            public static DriverMetadata PackageFileToCsv =>
                Csv<PackageFileRecord>(CatalogScanDriverType.PackageFileToCsv) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.Nupkg,
                    Dependencies = [CatalogScanDriverType.LoadPackageArchive],
                    LeafScanBatchSize = 10,
                };

            public static DriverMetadata PackageIconToCsv =>
                Csv<PackageIcon>(CatalogScanDriverType.PackageIconToCsv) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.Icon,

                    // Changes to the hosted icon for a package occur along with a catalog update, even package with icon
                    // URL (non-embedded icon) because the Catalog2Icon job follows the catalog. The data could be unstable
                    // if NuGet Insights runs before Catalog2Icon does (unlikely) or if the Magick.NET dependency is
                    // updated. In that case, the driver can be manually rerun with the "Reset" button on the admin panel.
                    UpdatedOutsideOfCatalog = false,
                };

            public static DriverMetadata PackageLicenseToCsv =>
                Csv<PackageLicense>(CatalogScanDriverType.PackageLicenseToCsv) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.License,

                    // If an SPDX support license becomes deprecated, the results of this driver will change when the
                    // NuGet.Package dependency is updated. This is rare, so we won't reprocess.
                    UpdatedOutsideOfCatalog = false,
                };

            public static DriverMetadata PackageManifestToCsv =>
                Csv<PackageManifestRecord>(CatalogScanDriverType.PackageManifestToCsv) with
                {
                    Dependencies = [CatalogScanDriverType.LoadPackageManifest],
                };

            public static DriverMetadata PackageReadmeToCsv =>
                Csv<PackageReadme>(CatalogScanDriverType.PackageReadmeToCsv) with
                {
                    Dependencies = [CatalogScanDriverType.LoadPackageReadme],
                };

            public static DriverMetadata PackageSignatureToCsv =>
                Csv<PackageSignature>(CatalogScanDriverType.PackageSignatureToCsv) with
                {
                    Dependencies = [CatalogScanDriverType.LoadPackageArchive],
                };

            // processes individual IDs not versions, needs a "latest leaves" step to dedupe versions
            public static DriverMetadata PackageVersionToCsv =>
                Csv<PackageVersionRecord>(CatalogScanDriverType.PackageVersionToCsv) with
                {
                    OnlyLatestLeavesSupport = true,
                    BucketRangeSupport = false,
                    Dependencies = [CatalogScanDriverType.LoadPackageVersion],
                    GetBucketKey = GetIdBucketKey,
                };

            public static DriverMetadata SymbolPackageArchiveToCsv =>
                Csv<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>(CatalogScanDriverType.SymbolPackageArchiveToCsv) with
                {
                    Dependencies = [CatalogScanDriverType.SymbolPackageFileToCsv],
                };

            public static DriverMetadata SymbolPackageFileToCsv =>
                Csv<SymbolPackageFileRecord>(CatalogScanDriverType.SymbolPackageFileToCsv) with
                {
                    DownloadedPackageAssets = DownloadedPackageAssets.Snupkg,
                    Dependencies = [CatalogScanDriverType.LoadSymbolPackageArchive],
                    LeafScanBatchSize = 10,
                };
        }
    }
}
