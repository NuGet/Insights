// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker
{
    public class CatalogScanCursorService
    {
        public const string FlatContainerCursorName = "NuGet.org flat container";

        private delegate Task<DateTimeOffset> GetCursorValue(CatalogScanCursorService service);

        private static readonly CatalogScanDriverType FlatContainer = (CatalogScanDriverType)int.MinValue + 1;

        private static readonly HashSet<CatalogScanDriverType> ValidDriverTypes = Enum
            .GetValues(typeof(CatalogScanDriverType))
            .Cast<CatalogScanDriverType>()
            .Except(new[]
            {
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScan,
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId,
            })
            .ToHashSet();

        private static readonly IReadOnlyDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> Dependencies = new Dictionary<CatalogScanDriverType, CatalogScanDriverType[]>
        {
            {
                CatalogScanDriverType.BuildVersionSet,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.LoadLatestPackageLeaf,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.LoadBucketedPackage,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.LoadPackageArchive,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.LoadSymbolPackageArchive,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.LoadPackageManifest,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.LoadPackageReadme,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.LoadPackageVersion,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.PackageArchiveToCsv,
                new[] { CatalogScanDriverType.LoadPackageArchive, CatalogScanDriverType.PackageAssemblyToCsv }
            },
            {
                CatalogScanDriverType.SymbolPackageArchiveToCsv,
                new[] { CatalogScanDriverType.LoadSymbolPackageArchive }
            },
            {
                CatalogScanDriverType.PackageAssetToCsv,
                new[] { CatalogScanDriverType.LoadPackageArchive }
            },
            {
                CatalogScanDriverType.PackageAssemblyToCsv,
                new[] { FlatContainer }
            },
#if ENABLE_CRYPTOAPI
            {
                CatalogScanDriverType.PackageCertificateToCsv,
                new[] { CatalogScanDriverType.LoadPackageArchive }
            },
#endif
            {
                CatalogScanDriverType.PackageSignatureToCsv,
                new[] { CatalogScanDriverType.LoadPackageArchive }
            },
            {
                CatalogScanDriverType.PackageManifestToCsv,
                new[] { CatalogScanDriverType.LoadPackageManifest }
            },
            {
                CatalogScanDriverType.PackageReadmeToCsv,
                new[] { CatalogScanDriverType.LoadPackageReadme }
            },
            {
                CatalogScanDriverType.PackageLicenseToCsv,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.PackageVersionToCsv,
                new[] { CatalogScanDriverType.LoadPackageVersion }
            },
#if ENABLE_NPE
            {
                CatalogScanDriverType.NuGetPackageExplorerToCsv,
                new[] { FlatContainer }
            },
#endif
            {
                CatalogScanDriverType.PackageCompatibilityToCsv,
                new[] { CatalogScanDriverType.LoadPackageArchive, CatalogScanDriverType.LoadPackageManifest }
            },
            {
                CatalogScanDriverType.PackageIconToCsv,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.CatalogDataToCsv,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.PackageContentToCsv,
                new[] { CatalogScanDriverType.LoadPackageArchive }
            },
        }.ToDictionary(x => x.Key, x => (IReadOnlyList<CatalogScanDriverType>)x.Value.ToList());

        private static readonly IReadOnlyDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> Dependents = Dependencies
            .SelectMany(x => x.Value.Select(y => new { Dependent = x.Key, Dependency = y }))
            .Where(x => ValidDriverTypes.Contains(x.Dependency))
            .GroupBy(x => x.Dependency, x => x.Dependent)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<CatalogScanDriverType>)x.ToList());

        private readonly CursorStorageService _cursorStorageService;
        private readonly IRemoteCursorClient _remoteCursorClient;
        private readonly ILogger<CatalogScanCursorService> _logger;

        public CatalogScanCursorService(
            CursorStorageService cursorStorageService,
            IRemoteCursorClient remoteCursorClient,
            ILogger<CatalogScanCursorService> logger)
        {
            _cursorStorageService = cursorStorageService;
            _remoteCursorClient = remoteCursorClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _cursorStorageService.InitializeAsync();
        }

        public static IReadOnlyList<CatalogScanDriverType> StartableDriverTypes { get; } =
            ValidDriverTypes
                .Order()
                .ToList();

        public static string GetCursorName(CatalogScanDriverType driverType)
        {
            return $"CatalogScan-{driverType}";
        }

        public async Task<CursorTableEntity> SetCursorAsync(CatalogScanDriverType driverType, DateTimeOffset value)
        {
            var entity = await _cursorStorageService.GetOrCreateAsync(GetCursorName(driverType), value);
            if (entity.Value != value)
            {
                entity.Value = value;
                await _cursorStorageService.UpdateAsync(entity);
            }

            return entity;
        }

        public async Task SetAllCursorsAsync(IEnumerable<CatalogScanDriverType> driverTypes, DateTimeOffset value)
        {
            var cursorNames = driverTypes.Select(GetCursorName).ToList();
            var cursors = await _cursorStorageService.GetOrCreateAllAsync(cursorNames, value);
            var cursorsToUpdate = cursors.Where(x => x.Value != value).ToList();
            foreach (var cursor in cursorsToUpdate)
            {
                cursor.Value = value;
            }

            await _cursorStorageService.UpdateAllAsync(cursorsToUpdate);
        }

        public async Task SetAllCursorsAsync(DateTimeOffset value)
        {
            await SetAllCursorsAsync(StartableDriverTypes, value);
        }

        public async Task<Dictionary<CatalogScanDriverType, CursorTableEntity>> GetCursorsAsync()
        {
            var nameToType = StartableDriverTypes.ToDictionary(GetCursorName);
            var cursors = await _cursorStorageService.GetOrCreateAllAsync(nameToType.Keys.ToList());
            return cursors.ToDictionary(x => nameToType[x.Name]);
        }

        public async Task<CursorTableEntity> GetCursorAsync(CatalogScanDriverType driverType)
        {
            return await _cursorStorageService.GetOrCreateAsync(GetCursorName(driverType));
        }

        public async Task<DateTimeOffset> GetCursorValueAsync(CatalogScanDriverType driverType)
        {
            var entity = await GetCursorAsync(driverType);
            return entity.Value;
        }

        public async Task<DateTimeOffset> GetSourceMaxAsync()
        {
            (var _, var value) = await GetDependencyValueAsync(FlatContainer);
            return value;
        }

        public async Task<KeyValuePair<string, DateTimeOffset>> GetDependencyMaxAsync(CatalogScanDriverType driverType)
        {
            string dependencyName = null;
            var max = DateTimeOffset.MaxValue;

            if (!Dependencies.TryGetValue(driverType, out var dependencies))
            {
                throw new InvalidOperationException($"No dependencies are defined for catalog scan driver {driverType}.");
            }

            foreach (var dependency in dependencies)
            {
                (var name, var dependencyMax) = await GetDependencyValueAsync(dependency);
                if (max > dependencyMax)
                {
                    dependencyName = name;
                    max = dependencyMax;
                }
            }

            return KeyValuePair.Create(dependencyName, max);
        }

        public static IReadOnlyList<IReadOnlyList<CatalogScanDriverType>> GetParallelBatches(
            Func<CatalogScanDriverType, bool> isDesired,
            Func<CatalogScanDriverType, bool> isRejected)
        {
            var isOrDependsOnIncluded = StartableDriverTypes
                .Where(isDesired)
                .SelectMany(GetTransitiveClosure)
                .ToList();

            // key: driver, value: direct dependencies
            var graph = new Dictionary<CatalogScanDriverType, HashSet<CatalogScanDriverType>>();
            foreach (var self in StartableDriverTypes)
            {
                foreach (var type in GetTransitiveClosure(self))
                {
                    if (isRejected(type) || graph.ContainsKey(type))
                    {
                        continue;
                    }

                    graph.Add(self, GetDependencies(self).ToHashSet());
                }
            }

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
                batch.IntersectWith(isOrDependsOnIncluded);
                if (batch.Count > 0)
                {
                    batches.Add(batch.OrderBy(x => x.ToString()).ToList());
                }
            }

            return batches;
        }

        private static ConcurrentDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> TransitiveClosureCache = new();

        public static IReadOnlyList<CatalogScanDriverType> GetTransitiveClosure(CatalogScanDriverType driverType)
        {
            return TransitiveClosureCache.GetOrAdd(driverType, driverType =>
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
                return output;
            });
        }

        private static ConcurrentDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> DependenciesCache = new();

        public static IReadOnlyList<CatalogScanDriverType> GetDependencies(CatalogScanDriverType driverType)
        {
            return DependenciesCache.GetOrAdd(driverType, driverType =>
            {
                var edges = GetEdges(driverType, Dependencies);
                return edges.Intersect(ValidDriverTypes).ToList();
            });
        }

        private static ConcurrentDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> DependentsCache = new();

        public static IReadOnlyList<CatalogScanDriverType> GetDependents(CatalogScanDriverType driverType)
        {
            return DependentsCache.GetOrAdd(driverType, driverType => GetEdges(driverType, Dependents));
        }

        private static Lazy<IReadOnlyList<CatalogScanDriverType>> FlatContainerDependentsCache = new Lazy<IReadOnlyList<CatalogScanDriverType>>(() =>
        {
            var dependents = new List<CatalogScanDriverType>();
            foreach ((var driverType, var dependencies) in Dependencies)
            {
                if (dependencies.Contains(FlatContainer))
                {
                    dependents.Add(driverType);
                }
            }

            return dependents;
        });

        public static IReadOnlyList<CatalogScanDriverType> GetFlatContainerDependents()
        {
            return FlatContainerDependentsCache.Value;
        }

        private static IReadOnlyList<CatalogScanDriverType> GetEdges(
            CatalogScanDriverType driverType,
            IReadOnlyDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> typeToEdges)
        {
            if (ValidDriverTypes.Contains(driverType))
            {
                if (typeToEdges.TryGetValue(driverType, out var dependents))
                {
                    return dependents;
                }
                else
                {
                    return Array.Empty<CatalogScanDriverType>();
                }
            }
            else
            {
                throw new ArgumentException("The provided driver type is not valid.", nameof(driverType));
            }
        }

        private async Task<(string Name, DateTimeOffset Value)> GetDependencyValueAsync(CatalogScanDriverType driverType)
        {
            if (driverType == FlatContainer)
            {
                return (FlatContainerCursorName, await _remoteCursorClient.GetFlatContainerAsync());
            }
            else if (ValidDriverTypes.Contains(driverType))
            {
                return (driverType.ToString(), await GetCursorValueAsync(driverType));
            }
            else
            {
                throw new ArgumentException("The provided driver type is not valid.", nameof(driverType));
            }
        }
    }
}
