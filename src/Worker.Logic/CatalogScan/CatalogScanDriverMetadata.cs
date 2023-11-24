// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Insights.Worker
{
    public static class CatalogScanDriverMetadata
    {
        /// <summary>
        /// Add a new item in this array when you introduce a new driver. Consider looking at the definition for a
        /// similar driver and 
        /// </summary>
        private static readonly IReadOnlyList<DriverMetadata> AllMetadata = new[]
        {
            // only needs catalog pages, not leaves
            OnlyCatalogRangeDriver(CatalogScanDriverType.BuildVersionSet),

            // needs all catalog leaves
            OnlyCatalogRangeDriver(CatalogScanDriverType.CatalogDataToCsv)
                with { DefaultMin = CatalogClient.NuGetOrgMin },

            // uses find latest driver, only reads catalog pages
            OnlyCatalogRangeDriver(CatalogScanDriverType.LoadBucketedPackage),

            // uses find latest driver, only reads catalog pages
            OnlyCatalogRangeDriver(CatalogScanDriverType.LoadLatestPackageLeaf),

            Default(CatalogScanDriverType.LoadPackageArchive),

            Default(CatalogScanDriverType.LoadPackageManifest),

            Default(CatalogScanDriverType.LoadPackageReadme),

            // internally uses find latest driver
            Default(CatalogScanDriverType.LoadPackageVersion)
                with { OnlyLatestLeavesSupport = false },

            Default(CatalogScanDriverType.LoadSymbolPackageArchive),

#if ENABLE_NPE
            Default(CatalogScanDriverType.NuGetPackageExplorerToCsv),
#endif

            Default(CatalogScanDriverType.PackageArchiveToCsv)
                with { Dependencies = [CatalogScanDriverType.LoadPackageArchive, CatalogScanDriverType.PackageAssemblyToCsv] },

            Default(CatalogScanDriverType.PackageAssemblyToCsv),

            Default(CatalogScanDriverType.PackageAssetToCsv)
                with { Dependencies = [CatalogScanDriverType.LoadPackageArchive] },

#if ENABLE_CRYPTOAPI
            Default(CatalogScanDriverType.PackageCertificateToCsv)
                with { Dependencies = [CatalogScanDriverType.LoadPackageArchive] },
#endif

            Default(CatalogScanDriverType.PackageCompatibilityToCsv)
                with { Dependencies = [CatalogScanDriverType.LoadPackageArchive, CatalogScanDriverType.LoadPackageManifest] },

            Default(CatalogScanDriverType.PackageContentToCsv)
                with { Dependencies = [CatalogScanDriverType.LoadPackageArchive] },

            Default(CatalogScanDriverType.PackageIconToCsv),

            Default(CatalogScanDriverType.PackageLicenseToCsv),

            Default(CatalogScanDriverType.PackageManifestToCsv)
                with { Dependencies = [CatalogScanDriverType.LoadPackageManifest] },

            Default(CatalogScanDriverType.PackageReadmeToCsv)
                with { Dependencies = [CatalogScanDriverType.LoadPackageReadme] },

            Default(CatalogScanDriverType.PackageSignatureToCsv)
                with { Dependencies = [CatalogScanDriverType.LoadPackageArchive] },
            
            // processes individual IDs not versions, needs a "latest leaves" step to dedupe versions
            OnlyCatalogRangeDriver(CatalogScanDriverType.PackageVersionToCsv)
                with { OnlyLatestLeavesSupport = true, Dependencies = [CatalogScanDriverType.LoadPackageVersion] },

            Default(CatalogScanDriverType.SymbolPackageArchiveToCsv)
                with { Dependencies = [CatalogScanDriverType.LoadSymbolPackageArchive] },
        };

        /// <summary>
        /// This method is implemented to give space for a full explanation of why or why not the driver is considered
        /// for having data updated outside of the catalog. If you're not sure whether to update this, just leave it
        /// alone.
        /// </summary>
        private static bool UpdatesOutsideOfCatalog(CatalogScanDriverType type)
        {
            return type switch
            {
                // README is not always embedded in the package and can be updated without a catalog update.
                CatalogScanDriverType.LoadPackageReadme => true,

                // Symbol package files (.snupkg) can be replaced and removed without a catalog update.
                CatalogScanDriverType.LoadSymbolPackageArchive => true,

#if ENABLE_NPE
                // Internally the NPE analysis APIs read symbols from the Microsoft and NuGet.org symbol servers. This
                // means that the results are unstable for a similar reason as LoadSymbolPackageArchive. Additionally,
                // some analysis times out (NuGetPackageExplorerResultType.Timeout). However this driver is relatively
                // costly and slow to run. Therefore we won't consider it for reprocessing.
                CatalogScanDriverType.NuGetPackageExplorerToCsv => false,
#endif

                // This driver uses a compatibility map baked into NuGet/NuGetGallery which uses the NuGet.Frameworks
                // package for framework compatibility. We could choose to periodically reprocess package compatibility
                // so that changes in TFM mapping and computed frameworks automatically get picked up. For now, we won't
                // do that and force a manual "Reset" operation from the admin panel to recompute all compatibilities.
                // The main part of this data that changes is computed compatibility. Newly supported frameworks can
                // also lead to changes in results, but this would take a package owner guessing or colliding with this
                // new framework in advance, leading to an "unsupported" framework reparsing as a supported framework.
                CatalogScanDriverType.PackageCompatibilityToCsv => false,

                // Similar to PackageCompatibilityToCsv, an unsupported framework (or an existing framework) can become
                // supported or change its interpretion over time. This is pretty unlikely so we don't reprocess
                // this driver.
                CatalogScanDriverType.PackageAssetToCsv => false,

#if ENABLE_CRYPTOAPI
                // Certificate data is not stable because certificates can expire or be revoked. Also, certificate
                // chain resolution is non-deterministic, so different intermediate certificates can be resolved over
                // time. Despite this, the changes are not to significant over time so we won't reprocess.
                CatalogScanDriverType.PackageCertificateToCsv => false,
#endif

                // If an SPDX support license becomes deprecated, the results of this driver will change when the
                // NuGet.Package dependency is updated. This is rare, so we won't reprocess.
                CatalogScanDriverType.PackageLicenseToCsv => false,

                // Changes to the hosted icon for a package occur along with a catalog update, even package with icon
                // URL (non-embedded icon) because the Catalog2Icon job follows the catalog. The data could be unstable
                // if NuGet Insights runs before Catalog2Icon does (unlikely) or if the Magick.NET dependency is
                // updated. In that case, the driver can be manually rerun with the "Reset" button on the admin panel.
                CatalogScanDriverType.PackageIconToCsv => false,

                _ => false
            };
        }

        private record DriverMetadata(
            CatalogScanDriverType Type,

            // This nullable boolean (tri-state) has the following meanings:
            // - null: the driver type supports run with or without the latest leaves scan
            // - false: the driver type cannot run with "only latest leaves = false"
            // - true: the driver type can only run with "only latest leaves = true"
            bool? OnlyLatestLeavesSupport,

            // This boolean has the following meanings:
            // - false: bucket range scans are not supported, only catalog commit ranges can be used
            // - true: bucket range scans are supported in addition to catalog commit range scans
            bool BucketRangeSupport,

            // This boolean has the following meanings:
            // - false: to properly track the updates of data ingested by this driver, we only need to update the data
            //          when there is a catalog leaf
            // - true: the data tracked by the driver can change without a catalog leaf being emitted, a periodic check
            //         for changed data is desired
            bool UpdatedOutsideOfCatalog,

            // The default minimum value to initialize this driver's cursor to. Most of the time this is
            // CatalogClient.NuGetOrgMinDeleted since this allows all deleted packages to be seen. If the driver needs
            // to see literally all catalog leaves (including the old duplicates), it could be CatalogClient.NuGetOrgMin.
            DateTimeOffset DefaultMin,

            // The other drivers that this driver depends on. The driver's cursor will not go beyond the dependency
            // cursors.
            IReadOnlyList<CatalogScanDriverType> Dependencies);

        /// <summary>
        /// A driver that only supports catalog ranges and no bucket ranges. Typically this driver processes catalog
        /// pages but not catalog leaves.
        /// </summary>
        private static DriverMetadata OnlyCatalogRangeDriver(CatalogScanDriverType type)
        {
            return new DriverMetadata(
                Type: type,
                OnlyLatestLeavesSupport: false,
                BucketRangeSupport: false,
                UpdatedOutsideOfCatalog: UpdatesOutsideOfCatalog(type),
                DefaultMin: CatalogClient.NuGetOrgMinDeleted,
                Dependencies: Array.Empty<CatalogScanDriverType>());
        }

        /// <summary>
        /// A driver that supports bucket range processing and catalog range processing. It can optionally use a "find
        /// latest" scan to eliminate duplicate package processing. This should probably be used for new drivers that
        /// generate some CSV records per package or load data into storage (e.g. Azure Table Storage) per package.
        /// </summary>
        private static DriverMetadata Default(CatalogScanDriverType type)
        {
            return new DriverMetadata(
                Type: type,
                OnlyLatestLeavesSupport: null,
                BucketRangeSupport: true,
                UpdatedOutsideOfCatalog: UpdatesOutsideOfCatalog(type),
                DefaultMin: CatalogClient.NuGetOrgMinDeleted,
                Dependencies: Array.Empty<CatalogScanDriverType>());
        }

        private static readonly FrozenSet<CatalogScanDriverType> ValidDriverTypes = Enum
            .GetValues(typeof(CatalogScanDriverType))
            .Cast<CatalogScanDriverType>()
            .Except(new[]
            {
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScan,
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId,
            })
            .ToFrozenSet();

        private static readonly FrozenDictionary<CatalogScanDriverType, DriverMetadata> TypeToMetadata = AllMetadata
            .ToFrozenDictionary(x => x.Type);

        private static readonly FrozenDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> TypeToDependents = AllMetadata
            .SelectMany(x => x.Dependencies.Select(y => new { Dependent = x.Type, Dependency = y }))
            .GroupBy(x => x.Dependency, x => x.Dependent)
            .Select(x => new { Dependency = x.Key, Dependents = x.Order().ToList() })
            .Concat(ValidDriverTypes.Select(x => new { Dependency = x, Dependents = new List<CatalogScanDriverType>() }))
            .GroupBy(x => x.Dependency, x => x.Dependents)
            .ToFrozenDictionary(x => x.Key, x => (IReadOnlyList<CatalogScanDriverType>)x.SelectMany(y => y).Order().ToList());

        public static IReadOnlyList<CatalogScanDriverType> StartableDriverTypes { get; } = ValidDriverTypes
            .Order()
            .ToList();        

        private static readonly FrozenDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> TypeToTransitiveClosures = StartableDriverTypes
            .ToFrozenDictionary(x => x, driverType =>
            {
                var output = new List<CatalogScanDriverType>();
                var explored = new HashSet<CatalogScanDriverType>();
                var toExpand = new Queue<CatalogScanDriverType>();
                toExpand.Enqueue(driverType);

                while (toExpand.Count > 0)
                {
                    var next = toExpand.Dequeue();
                    if (explored.Add(next))
                    {
                        output.Add(next);

                        foreach (var dependency in GetDependencies(next))
                        {
                            toExpand.Enqueue(dependency);
                        }
                    }
                }

                output.Reverse();
                return (IReadOnlyList<CatalogScanDriverType>)output;
            });

        private static readonly IReadOnlyList<CatalogScanDriverType> FlatContainerDependents = AllMetadata
            .Where(x => x.Dependencies.Count == 0)
            .Select(x => x.Type)
            .Order()
            .ToList();

        private static T GetValue<T>(IReadOnlyDictionary<CatalogScanDriverType, T> lookup, CatalogScanDriverType driverType)
        {
            if (!lookup.TryGetValue(driverType, out var data))
            {
                throw new NotImplementedException($"The driver {driverType} is not fully implemented. Update the {nameof(CatalogScanDriverMetadata)} class.");
            }

            return data;
        }

        public static IReadOnlyList<IReadOnlyList<CatalogScanDriverType>> GetParallelBatches(
            IReadOnlySet<CatalogScanDriverType> desired,
            IReadOnlySet<CatalogScanDriverType> rejected)
        {
            // key: driver, value: direct dependencies
            var graph = new Dictionary<CatalogScanDriverType, HashSet<CatalogScanDriverType>>();
            foreach (var self in StartableDriverTypes)
            {
                foreach (var type in GetTransitiveClosure(self))
                {
                    if (rejected.Contains(type) || graph.ContainsKey(type))
                    {
                        continue;
                    }

                    graph.Add(self, GetDependencies(self).ToHashSet());
                }
            }

            var isOrDependsOnDesired = desired
                .SelectMany(GetTransitiveClosure)
                .ToHashSet();

            // Collect batches of drivers that can run in parallel.
            var batches = new List<IReadOnlyList<CatalogScanDriverType>>();
            while (graph.Count > 0)
            {
                var batch = graph.Where(pair => pair.Value.Count == 0).Select(x => x.Key).ToHashSet();
                if (batch.Count == 0)
                {
                    var unresolved = graph
                        .Select(pair => new { Driver = pair.Key, Missing = pair.Value.Where(x => !graph.ContainsKey(x)).Order().ToList() })
                        .Where(x => x.Missing.Count > 0)
                        .OrderBy(x => x.Driver)
                        .Select(x => $"{x.Driver} depends on {string.Join(", ", x.Missing)}")
                        .ToList();

                    throw new InvalidOperationException(
                        $"Check the {nameof(NuGetInsightsWorkerSettings)}.{nameof(NuGetInsightsWorkerSettings.DisabledDrivers)} option. " +
                        $"Some drivers are missing dependencies: {string.Join("; ", unresolved)}");
                }

                foreach (var type in batch)
                {
                    graph.Remove(type);
                }

                foreach (var dependencies in graph.Values)
                {
                    dependencies.ExceptWith(batch);
                }

                // Only keep drivers that need reprocessing or drivers that depend on a driver that needs reprocessing.
                batch.IntersectWith(isOrDependsOnDesired);
                if (batch.Count > 0)
                {
                    batches.Add(batch.OrderBy(x => x.ToString()).ToList());
                }
            }

            return batches;
        }

        public static bool? GetOnlyLatestLeavesSupport(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToMetadata, driverType).OnlyLatestLeavesSupport;
        }

        public static bool GetBucketRangeSupport(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToMetadata, driverType).BucketRangeSupport;
        }

        public static bool GetUpdatedOutsideOfCatalog(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToMetadata, driverType).UpdatedOutsideOfCatalog;
        }

        public static DateTimeOffset GetDefaultMin(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToMetadata, driverType).DefaultMin;
        }

        public static IReadOnlyList<CatalogScanDriverType> GetTransitiveClosure(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToTransitiveClosures, driverType);
        }

        public static IReadOnlyList<CatalogScanDriverType> GetDependencies(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToMetadata, driverType).Dependencies;
        }

        public static IReadOnlyList<CatalogScanDriverType> GetDependents(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToDependents, driverType);
        }

        public static IReadOnlyList<CatalogScanDriverType> GetFlatContainerDependents()
        {
            return FlatContainerDependents;
        }
    }
}
