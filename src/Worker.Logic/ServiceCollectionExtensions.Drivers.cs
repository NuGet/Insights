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
            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<CatalogLeafScan>, LatestCatalogLeafScanStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<CatalogLeafScan>>();

            // Internal_FindLatestCatalogLeafScanPerId
            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<CatalogLeafScanPerId>, LatestCatalogLeafScanPerIdStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<CatalogLeafScanPerId>>();

            // PackageCompatibilityToCsv
            serviceCollection.AddSingleton<IPackageFrameworkCompatibilityFactory>(new PackageFrameworkCompatibilityFactory());

            // BuildVersionSet
            serviceCollection.AddTransient<BuildVersionSetDriver>();
            serviceCollection.AddTransient<VersionSetAggregateStorageService>();
            serviceCollection.AddSingleton<VersionSetService>();
            serviceCollection.AddSingleton<IVersionSetProvider>(s => s.GetRequiredService<VersionSetService>());

            // LoadLatestPackageLeaf
            AddTableScan<LatestPackageLeaf>(serviceCollection);
            serviceCollection.AddTransient<LatestPackageLeafService>();
            serviceCollection.AddTransient<LatestPackageLeafStorageFactory>();
            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<LatestPackageLeaf>, LatestPackageLeafStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<LatestPackageLeaf>>();

            // LoadBucketedPackage
            AddTableScan<BucketedPackage>(serviceCollection);
            serviceCollection.AddTransient<BucketedPackageService>();
            serviceCollection.AddTransient<BucketedPackageStorageFactory>();
            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<BucketedPackage>, BucketedPackageStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<BucketedPackage>>();

            // LoadPackageArchive
            serviceCollection.AddTransient<LoadPackageArchiveDriver>();

            // LoadSymbolPackageArchive
            serviceCollection.AddTransient<LoadSymbolPackageArchiveDriver>();

            // LoadPackageManifest
            serviceCollection.AddTransient<LoadPackageManifestDriver>();

            // LoadPackageReadme
            serviceCollection.AddTransient<LoadPackageReadmeDriver>();

            // LoadPackageVersion
            serviceCollection.AddTransient<LoadPackageVersionDriver>();
            serviceCollection.AddTransient<PackageVersionStorageService>();
        }
    }
}
