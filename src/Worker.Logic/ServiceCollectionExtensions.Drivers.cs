// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.BuildVersionSet;
using NuGet.Insights.Worker.FindLatestCatalogLeafScan;
using NuGet.Insights.Worker.FindLatestCatalogLeafScanPerId;
using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.LoadLatestPackageLeaf;
using NuGet.Insights.Worker.LoadPackageArchive;
using NuGet.Insights.Worker.LoadPackageManifest;
using NuGet.Insights.Worker.LoadPackageReadme;
using NuGet.Insights.Worker.LoadPackageVersion;
using NuGet.Insights.Worker.LoadSymbolPackageArchive;
using NuGetGallery.Frameworks;

namespace NuGet.Insights.Worker
{
    public static partial class ServiceCollectionExtensions
    {
        /// <summary>
        /// Invoked via reflection in <see cref="AddNuGetInsightsWorker(IServiceCollection)"/>.
        /// </summary>
        private static void SetupDrivers(IServiceCollection serviceCollection)
        {
            // Internal_FindLatestCatalogLeafScan
            serviceCollection.AddSingleton<ILatestPackageLeafStorageFactory<CatalogLeafScan>, LatestCatalogLeafScanStorageFactory>();
            serviceCollection.AddSingleton<FindLatestLeafDriver<CatalogLeafScan>>();

            // Internal_FindLatestCatalogLeafScanPerId
            serviceCollection.AddSingleton<ILatestPackageLeafStorageFactory<CatalogLeafScanPerId>, LatestCatalogLeafScanPerIdStorageFactory>();
            serviceCollection.AddSingleton<FindLatestLeafDriver<CatalogLeafScanPerId>>();

            // PackageCompatibilityToCsv
            serviceCollection.AddSingleton<IPackageFrameworkCompatibilityFactory>(new PackageFrameworkCompatibilityFactory());

            // BuildVersionSet
            serviceCollection.AddSingleton<BuildVersionSetDriver>();
            serviceCollection.AddSingleton<VersionSetAggregateStorageService>();
            serviceCollection.AddSingleton<VersionSetService>();
            serviceCollection.AddSingleton<IVersionSetProvider>(s => s.GetRequiredService<VersionSetService>());

            // LoadLatestPackageLeaf
            AddTableScan<LatestPackageLeaf>(serviceCollection);
            serviceCollection.AddSingleton<LatestPackageLeafService>();
            serviceCollection.AddSingleton<LatestPackageLeafStorageFactory>();
            serviceCollection.AddSingleton<ILatestPackageLeafStorageFactory<LatestPackageLeaf>, LatestPackageLeafStorageFactory>();
            serviceCollection.AddSingleton<FindLatestLeafDriver<LatestPackageLeaf>>();

            // LoadBucketedPackage
            AddTableScan<BucketedPackage>(serviceCollection);
            serviceCollection.AddSingleton<BucketedPackageService>();
            serviceCollection.AddSingleton<BucketedPackageStorageFactory>();
            serviceCollection.AddSingleton<ILatestPackageLeafStorageFactory<BucketedPackage>, BucketedPackageStorageFactory>();
            serviceCollection.AddSingleton<FindLatestLeafDriver<BucketedPackage>>();

            // LoadPackageArchive
            serviceCollection.AddSingleton<LoadPackageArchiveDriver>();

            // LoadSymbolPackageArchive
            serviceCollection.AddSingleton<LoadSymbolPackageArchiveDriver>();

            // LoadPackageManifest
            serviceCollection.AddSingleton<LoadPackageManifestDriver>();

            // LoadPackageReadme
            serviceCollection.AddSingleton<LoadPackageReadmeDriver>();

            // LoadPackageVersion
            serviceCollection.AddSingleton<LoadPackageVersionDriver>();
            serviceCollection.AddSingleton<PackageVersionStorageService>();
        }
    }
}
