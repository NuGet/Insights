// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class CatalogScanServiceTest
    {
        private partial class DriverInfo
        {
            public static DriverInfo BuildVersionSet => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = false,
                SupportsBucketRangeProcessing = false,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo CatalogDataToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMin,
                OnlyLatestLeavesSupport = false,
                SupportsBucketRangeProcessing = false,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo LoadLatestPackageLeaf => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = false,
                SupportsBucketRangeProcessing = false,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo LoadBucketedPackage => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = false,
                SupportsBucketRangeProcessing = false,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo LoadPackageArchive => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo LoadSymbolPackageArchive => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo LoadPackageManifest => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo LoadPackageReadme => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo LoadPackageVersion => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = false,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo PackageAssemblyToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                },
            };

            public static DriverInfo PackageAssetToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                },
            };

            public static DriverInfo PackageArchiveToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.PackageFileToCsv, x);
                },
            };

            public static DriverInfo PackageCompatibilityToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, x);
                },
            };

            public static DriverInfo PackageContentToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                },
            };

            public static DriverInfo PackageFileToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                },
            };

            public static DriverInfo PackageLicenseToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo PackageIconToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };

            public static DriverInfo PackageManifestToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, x);
                },
            };

            public static DriverInfo PackageReadmeToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageReadme, x);
                },
            };

            public static DriverInfo PackageSignatureToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, x);
                },
            };

            public static DriverInfo PackageVersionToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = true,
                SupportsBucketRangeProcessing = false,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, x);
                },
            };

            public static DriverInfo SymbolPackageArchiveToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.SymbolPackageFileToCsv, x);
                },
            };

            public static DriverInfo SymbolPackageFileToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = async (self, x) =>
                {
                    await self.SetCursorAsync(CatalogScanDriverType.LoadSymbolPackageArchive, x);
                },
            };
        }
    }
}
