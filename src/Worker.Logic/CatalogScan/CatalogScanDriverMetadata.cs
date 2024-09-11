// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Frozen;
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

#if ENABLE_CRYPTOAPI
using NuGet.Insights.Worker.PackageCertificateToCsv;
#endif

#if ENABLE_NPE
using NuGet.Insights.Worker.NuGetPackageExplorerToCsv;
#endif

#nullable enable

namespace NuGet.Insights.Worker
{
    public delegate string GetBucketKey(string lowerId, string normalizedVersion);

    public static class CatalogScanDriverMetadata
    {
        /// <summary>
        /// Add a new item in this array when you introduce a new driver. Consider looking at the definition for a
        /// similar driver and 
        /// </summary>
        private static readonly IReadOnlyList<DriverMetadata> AllMetadata = new[]
        {
            // only needs catalog pages, not leaves
            OnlyCatalogRange<BuildVersionSetDriver>(CatalogScanDriverType.BuildVersionSet),

            // needs all catalog leaves
            Csv<PackageDeprecationRecord, PackageVulnerabilityRecord, CatalogLeafItemRecord>(CatalogScanDriverType.CatalogDataToCsv) with
            {
                DefaultMin = CatalogClient.NuGetOrgMin,
                OnlyLatestLeavesSupport = false,
                BucketRangeSupport = false,
                GetBucketKey = null,
            },

            // uses find latest driver, only reads catalog pages
            OnlyCatalogRange<FindLatestLeafDriver<BucketedPackage>>(CatalogScanDriverType.LoadBucketedPackage),

            // uses find latest driver, only reads catalog pages
            OnlyCatalogRange<FindLatestLeafDriver<LatestPackageLeaf>>(CatalogScanDriverType.LoadLatestPackageLeaf),

            Default<LoadPackageArchiveDriver>(CatalogScanDriverType.LoadPackageArchive),

            Default<LoadPackageManifestDriver>(CatalogScanDriverType.LoadPackageManifest),

            Default<LoadPackageReadmeDriver>(CatalogScanDriverType.LoadPackageReadme) with
            {
                // README is not always embedded in the package and can be updated without a catalog update.
                UpdatedOutsideOfCatalog = true,
            },

            // internally uses find latest driver
            Default<LoadPackageVersionDriver>(CatalogScanDriverType.LoadPackageVersion) with
            {
                OnlyLatestLeavesSupport = false,
            },

            Default<LoadSymbolPackageArchiveDriver>(CatalogScanDriverType.LoadSymbolPackageArchive) with
            {
                // Symbol package files (.snupkg) can be replaced and removed without a catalog update.
                UpdatedOutsideOfCatalog = true,
            },

#if ENABLE_NPE
            Csv<NuGetPackageExplorerRecord, NuGetPackageExplorerFile>(CatalogScanDriverType.NuGetPackageExplorerToCsv) with
            {
                // Internally the NPE analysis APIs read symbols from the Microsoft and NuGet.org symbol servers. This
                // means that the results are unstable for a similar reason as LoadSymbolPackageArchive. Additionally,
                // some analysis times out (NuGetPackageExplorerResultType.Timeout). However this driver is relatively
                // costly and slow to run. Therefore we won't consider it for reprocessing.
                UpdatedOutsideOfCatalog = false,
            },
#endif

            Csv<PackageArchiveRecord, PackageArchiveEntry>(CatalogScanDriverType.PackageArchiveToCsv) with
            {
                Dependencies = [CatalogScanDriverType.PackageFileToCsv],
            },

            Csv<PackageAssembly>(CatalogScanDriverType.PackageAssemblyToCsv) with
            {
                Dependencies = [CatalogScanDriverType.LoadPackageArchive],
            },

            Csv<PackageAsset>(CatalogScanDriverType.PackageAssetToCsv) with
            {
                // Similar to PackageCompatibilityToCsv, an unsupported framework (or an existing framework) can become
                // supported or change its interpretion over time. This is pretty unlikely so we don't reprocess
                // this driver.
                UpdatedOutsideOfCatalog = false,
                Dependencies = [CatalogScanDriverType.LoadPackageArchive],
            },

#if ENABLE_CRYPTOAPI
            BatchCsv<PackageCertificateRecord, CertificateRecord>(CatalogScanDriverType.PackageCertificateToCsv) with
            {
                // Certificate data is not stable because certificates can expire or be revoked. Also, certificate
                // chain resolution is non-deterministic, so different intermediate certificates can be resolved over
                // time. Despite this, the changes are not to significant over time so we won't reprocess.
                UpdatedOutsideOfCatalog = false,
                Dependencies = [CatalogScanDriverType.LoadPackageArchive],
            },
#endif

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
            },

            Csv<PackageContent>(CatalogScanDriverType.PackageContentToCsv) with
            {
                Dependencies = [CatalogScanDriverType.LoadPackageArchive],
            },

            Csv<PackageFileRecord>(CatalogScanDriverType.PackageFileToCsv) with
            {
                Dependencies = [CatalogScanDriverType.LoadPackageArchive],
            },

            Csv<PackageIcon>(CatalogScanDriverType.PackageIconToCsv) with
            {
                // Changes to the hosted icon for a package occur along with a catalog update, even package with icon
                // URL (non-embedded icon) because the Catalog2Icon job follows the catalog. The data could be unstable
                // if NuGet Insights runs before Catalog2Icon does (unlikely) or if the Magick.NET dependency is
                // updated. In that case, the driver can be manually rerun with the "Reset" button on the admin panel.
                UpdatedOutsideOfCatalog = false,
            },

            Csv<PackageLicense>(CatalogScanDriverType.PackageLicenseToCsv) with
            {
                // If an SPDX support license becomes deprecated, the results of this driver will change when the
                // NuGet.Package dependency is updated. This is rare, so we won't reprocess.
                UpdatedOutsideOfCatalog = false,
            },

            Csv<PackageManifestRecord>(CatalogScanDriverType.PackageManifestToCsv) with
            {
                Dependencies = [CatalogScanDriverType.LoadPackageManifest],
            },

            Csv<PackageReadme>(CatalogScanDriverType.PackageReadmeToCsv) with
            {
                Dependencies = [CatalogScanDriverType.LoadPackageReadme],
            },

            Csv<PackageSignature>(CatalogScanDriverType.PackageSignatureToCsv) with
            {
                Dependencies = [CatalogScanDriverType.LoadPackageArchive],
            },
            
            // processes individual IDs not versions, needs a "latest leaves" step to dedupe versions
            Csv<PackageVersionRecord>(CatalogScanDriverType.PackageVersionToCsv) with
            {
                OnlyLatestLeavesSupport = true,
                BucketRangeSupport = false,
                Dependencies = [CatalogScanDriverType.LoadPackageVersion],
                GetBucketKey = GetIdBucketKey,
            },

            Csv<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>(CatalogScanDriverType.SymbolPackageArchiveToCsv) with
            {
                Dependencies = [CatalogScanDriverType.SymbolPackageFileToCsv],
            },

            Csv<SymbolPackageFileRecord>(CatalogScanDriverType.SymbolPackageFileToCsv) with
            {
                Dependencies = [CatalogScanDriverType.LoadSymbolPackageArchive],
            },
        };

        private record DriverMetadata(
            // The type value, used for fast runtime comparisons
            CatalogScanDriverType Type,

            // The .NET type of the driver used for runtime processing. Implements ICatalogLeafScanBatchDriver or ICatalogLeafScanNonBatchDriver.
            Type RuntimeType,

            // Whether or not the runtime type is ICatalogLeafScanBatchDriver.
            bool IsBatchDriver,

            // This nullable boolean (tri-state) has the following meanings:
            // - null: the driver type supports run with or without the latest leaves scan
            // - true: the driver type can only run with "only latest leaves = true"
            // - false: the driver type can only run with "only latest leaves = false"
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
            IReadOnlyList<CatalogScanDriverType> Dependencies,

            // The function to get the bucket key for the latest leaf scan. Only used for drivers that have
            // OnlyLatestLeavesSupport set to null or true.
            GetBucketKey? GetBucketKey,

            // The type of CSV records that this driver produces.
            IReadOnlyList<Type>? CsvRecordTypes);

        /// <summary>
        /// A driver that only supports catalog ranges and no bucket ranges. Typically this driver processes catalog
        /// pages but not catalog leaves.
        /// </summary>
        private static DriverMetadata OnlyCatalogRange<T>(CatalogScanDriverType type) where T : ICatalogLeafScanBatchDriver
        {
            return new DriverMetadata(
                Type: type,
                RuntimeType: typeof(T),
                IsBatchDriver: true,
                OnlyLatestLeavesSupport: false,
                BucketRangeSupport: false,
                UpdatedOutsideOfCatalog: false,
                DefaultMin: CatalogClient.NuGetOrgMinDeleted,
                Dependencies: [],
                GetBucketKey: null,
                CsvRecordTypes: null);
        }

        private static DriverMetadata Csv<T>(CatalogScanDriverType type)
            where T : IAggregatedCsvRecord<T> => Csv(
                type,
                typeof(CatalogLeafScanToCsvNonBatchAdapter<>).MakeGenericType([typeof(T)]));

        private static DriverMetadata BatchCsv<T>(CatalogScanDriverType type)
            where T : IAggregatedCsvRecord<T> => Csv(
                type,
                typeof(CatalogLeafScanToCsvBatchAdapter<>).MakeGenericType([typeof(T)]));

        private static DriverMetadata Csv<T1, T2>(CatalogScanDriverType type)
            where T1 : IAggregatedCsvRecord<T1>
            where T2 : IAggregatedCsvRecord<T2> => Csv(
                type,
                typeof(CatalogLeafScanToCsvNonBatchAdapter<,>).MakeGenericType([typeof(T1), typeof(T2)]));

        private static DriverMetadata BatchCsv<T1, T2>(CatalogScanDriverType type)
            where T1 : IAggregatedCsvRecord<T1>
            where T2 : IAggregatedCsvRecord<T2> => Csv(
                type,
                typeof(CatalogLeafScanToCsvBatchAdapter<,>).MakeGenericType([typeof(T1), typeof(T2)]));

        private static DriverMetadata Csv<T1, T2, T3>(CatalogScanDriverType type)
            where T1 : IAggregatedCsvRecord<T1>
            where T2 : IAggregatedCsvRecord<T2>
            where T3 : IAggregatedCsvRecord<T3> => Csv(
                type,
                typeof(CatalogLeafScanToCsvNonBatchAdapter<,,>).MakeGenericType([typeof(T1), typeof(T2), typeof(T3)]));

        private static DriverMetadata BatchCsv<T1, T2, T3>(CatalogScanDriverType type)
            where T1 : IAggregatedCsvRecord<T1>
            where T2 : IAggregatedCsvRecord<T2>
            where T3 : IAggregatedCsvRecord<T3> => Csv(
                type,
                typeof(CatalogLeafScanToCsvBatchAdapter<,,>).MakeGenericType([typeof(T1), typeof(T2), typeof(T3)]));


        /// <summary>
        /// A driver that supports bucket range processing and catalog range processing. It can optionally use a "find
        /// latest" scan to eliminate duplicate package processing. This should probably be used for new drivers that
        /// generate some CSV records per package.
        /// </summary>
        private static DriverMetadata Csv(CatalogScanDriverType type, Type runtimeType)
        {
            var csvRecordTypes = runtimeType.GetGenericArguments();
            foreach (var recordType in csvRecordTypes)
            {
                if (!recordType.IsAssignableTo(typeof(IAggregatedCsvRecord<>).MakeGenericType(recordType)))
                {
                    throw new ArgumentException($"Record type {recordType.Name} is not assignable to {nameof(IAggregatedCsvRecord)}<{recordType.Name}>.", nameof(recordType));
                }
            }

            return new DriverMetadata(
                Type: type,
                RuntimeType: runtimeType,
                IsBatchDriver: true,
                OnlyLatestLeavesSupport: null,
                BucketRangeSupport: true,
                UpdatedOutsideOfCatalog: false,
                DefaultMin: CatalogClient.NuGetOrgMinDeleted,
                Dependencies: [],
                GetBucketKey: GetIdentityBucketKey,
                CsvRecordTypes: csvRecordTypes);
        }

        /// <summary>
        /// A driver that supports bucket range processing and catalog range processing. It can optionally use a "find
        /// latest" scan to eliminate duplicate package processing. This should probably be used for new drivers that
        /// generate some CSV records per package or load data into storage (e.g. Azure Table Storage) per package.
        /// </summary>
        private static DriverMetadata Default<T>(CatalogScanDriverType type) where T : ICatalogLeafScanBatchDriver
        {
            return new DriverMetadata(
                Type: type,
                RuntimeType: typeof(T),
                IsBatchDriver: true,
                OnlyLatestLeavesSupport: null,
                BucketRangeSupport: true,
                UpdatedOutsideOfCatalog: false,
                DefaultMin: CatalogClient.NuGetOrgMinDeleted,
                Dependencies: [],
                GetBucketKey: GetIdentityBucketKey,
                CsvRecordTypes: null);
        }

        private static string GetIdentityBucketKey(string lowerId, string normalizedVersion)
        {
            return PackageRecord.GetIdentity(lowerId, normalizedVersion);
        }

        private static string GetIdBucketKey(string lowerId, string normalizedVersion)
        {
            return lowerId;
        }

        private static readonly FrozenSet<CatalogScanDriverType> ValidDriverTypes = CatalogScanDriverType.AllTypes
            .Except([
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScan,
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId,
            ])
            .ToFrozenSet();

        private static readonly FrozenDictionary<CatalogScanDriverType, DriverMetadata> TypeToMetadata = AllMetadata
            .ToFrozenDictionary(x => x.Type);

        private static readonly FrozenDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> TypeToDependents = AllMetadata
            .SelectMany(x => x.Dependencies.Select(y => new { Dependent = x.Type, Dependency = y }))
            .GroupBy(x => x.Dependency, x => x.Dependent)
            .Select(x => new { Dependency = x.Key, Dependents = (IReadOnlyList<CatalogScanDriverType>)x.Order().ToList() })
            .Concat(ValidDriverTypes.Select(x => new { Dependency = x, Dependents = (IReadOnlyList<CatalogScanDriverType>)[] }))
            .GroupBy(x => x.Dependency, x => x.Dependents)
            .ToFrozenDictionary(x => x.Key, x => (IReadOnlyList<CatalogScanDriverType>)x.SelectMany(y => y).Order().ToList());

        private static FrozenDictionary<CatalogScanDriverType, (int Depth, int FinalOrder)> TopologicalOrder { get; } = GetTopologicalOrder();

        public static IReadOnlyList<CatalogScanDriverType> StartableDriverTypes { get; } = TopologicalOrder
            .OrderBy(x => x.Value.FinalOrder)
            .Select(x => x.Key)
            .ToList();

        public static IOrderedEnumerable<T> SortByTopologicalOrder<T>(IEnumerable<T> source, Func<T, CatalogScanDriverType> getDriverType)
        {
            return source.OrderBy(x =>
            {
                if (TopologicalOrder.TryGetValue(getDriverType(x), out var pair))
                {
                    return pair.FinalOrder;
                }

                return int.MaxValue;
            });
        }

        private static FrozenDictionary<CatalogScanDriverType, (int Depth, int FinalOrder)> GetTopologicalOrder()
        {
            var added = new HashSet<CatalogScanDriverType>();
            var remaining = ValidDriverTypes.ToDictionary(x => x, x => GetDependencies(x).ToHashSet());
            var driverTypeToDepth = new Dictionary<CatalogScanDriverType, int>();
            var depth = 0;

            while (remaining.Count > 0)
            {
                var isCovered = remaining
                    .Where(pair => pair.Value.Count == 0 || added.IsSupersetOf(pair.Value))
                    .Select(pair => pair.Key)
                    .ToList();

                if (isCovered.Count == 0)
                {
                    throw new InvalidOperationException("Unable to find topological sort. Missing dependencies.");
                }

                foreach (var driverType in isCovered)
                {
                    remaining.Remove(driverType);
                    added.Add(driverType);
                    driverTypeToDepth.Add(driverType, depth);
                }

                depth++;
            }

            var finalOrder = ValidDriverTypes
                .OrderBy(x => driverTypeToDepth[x])
                .ThenBy(x => x.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lookup = new Dictionary<CatalogScanDriverType, (int Depth, int FinalOrder)>();
            for (var i = 0; i < finalOrder.Count; i++)
            {
                var driverType = finalOrder[i];
                lookup.Add(driverType, (driverTypeToDepth[driverType], i));
            }

            return lookup.ToFrozenDictionary();
        }

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

        private static T GetValue<T>(FrozenDictionary<CatalogScanDriverType, T> lookup, CatalogScanDriverType driverType)
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

        public static IReadOnlyList<Type>? GetRecordTypes(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToMetadata, driverType).CsvRecordTypes;
        }

        public static Type GetRuntimeType(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToMetadata, driverType).RuntimeType;
        }

        public static bool IsBatchDriver(CatalogScanDriverType driverType)
        {
            return GetValue(TypeToMetadata, driverType).IsBatchDriver;
        }

        public static GetBucketKey GetBucketKeyFactory(CatalogScanDriverType driverType)
        {
            var value = GetValue(TypeToMetadata, driverType);
            if (value.OnlyLatestLeavesSupport == false)
            {
                throw new InvalidOperationException("Cannot get bucket key for a driver that does not support latest leaves.");
            }

            if (value.GetBucketKey is null)
            {
                throw new NotImplementedException();
            }

            return value.GetBucketKey;
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
