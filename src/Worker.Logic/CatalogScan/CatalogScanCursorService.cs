using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanCursorService
    {
        public const string CatalogCursorName = "NuGet.org catalog";
        public const string FlatContainerCursorName = "NuGet.org flat container";

        private delegate Task<DateTimeOffset> GetCursorValue(CatalogScanCursorService service);

        private static readonly CatalogScanDriverType Catalog = (CatalogScanDriverType)int.MinValue;
        private static readonly CatalogScanDriverType FlatContainer = Catalog + 1;

        private static readonly HashSet<CatalogScanDriverType> ValidDriverTypes = Enum
            .GetValues(typeof(CatalogScanDriverType))
            .Cast<CatalogScanDriverType>()
            .Except(new[]
            {
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScan,
                CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId,
            })
            .ToHashSet();
        private static readonly IReadOnlyList<CatalogScanDriverType> SortedDriverTypes = ValidDriverTypes
            .OrderBy(x => x)
            .ToList();

        private static readonly IReadOnlyDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> Dependencies = new Dictionary<CatalogScanDriverType, CatalogScanDriverType[]>
        {
            {
                CatalogScanDriverType.BuildVersionSet,
                new[] { Catalog }
            },
            {
                CatalogScanDriverType.CatalogLeafItemToCsv,
                new[] { Catalog }
            },
            {
                CatalogScanDriverType.LoadLatestPackageLeaf,
                new[] { Catalog }
            },
            {
                CatalogScanDriverType.LoadPackageArchive,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.LoadPackageManifest,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.LoadPackageVersion,
                new[] { Catalog }
            },
            {
                CatalogScanDriverType.PackageArchiveToCsv,
                new[] { CatalogScanDriverType.LoadPackageArchive, CatalogScanDriverType.PackageAssemblyToCsv }
            },
            {
                CatalogScanDriverType.PackageAssetToCsv,
                new[] { CatalogScanDriverType.LoadPackageArchive }
            },
            {
                CatalogScanDriverType.PackageAssemblyToCsv,
                new[] { FlatContainer }
            },
            {
                CatalogScanDriverType.PackageSignatureToCsv,
                new[] { CatalogScanDriverType.LoadPackageArchive }
            },
            {
                CatalogScanDriverType.PackageManifestToCsv,
                new[] { CatalogScanDriverType.LoadPackageManifest }
            },
            {
                CatalogScanDriverType.PackageVersionToCsv,
                new[] { CatalogScanDriverType.LoadPackageVersion }
            },
            {
                CatalogScanDriverType.NuGetPackageExplorerToCsv,
                new[] { FlatContainer, CatalogScanDriverType.LoadLatestPackageLeaf }
            },
        }.ToDictionary(x => x.Key, x => (IReadOnlyList<CatalogScanDriverType>)x.Value.ToList());

        private static readonly IReadOnlyDictionary<CatalogScanDriverType, IReadOnlyList<CatalogScanDriverType>> Dependents = Dependencies
            .SelectMany(x => x.Value.Select(y => new { Dependent = x.Key, Dependency = y }))
            .Where(x => ValidDriverTypes.Contains(x.Dependency))
            .GroupBy(x => x.Dependency, x => x.Dependent)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<CatalogScanDriverType>)x.ToList());

        private readonly CursorStorageService _cursorStorageService;
        private readonly IRemoteCursorClient _remoteCursorClient;

        public CatalogScanCursorService(
            CursorStorageService cursorStorageService,
            IRemoteCursorClient remoteCursorClient)
        {
            _cursorStorageService = cursorStorageService;
            _remoteCursorClient = remoteCursorClient;
        }

        public async Task InitializeAsync()
        {
            await _cursorStorageService.InitializeAsync();
        }

        public IReadOnlyList<CatalogScanDriverType> StartableDriverTypes => SortedDriverTypes;

        public string GetCursorName(CatalogScanDriverType driverType)
        {
            return $"CatalogScan-{driverType}";
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

        public IReadOnlyList<CatalogScanDriverType> GetDependencies(CatalogScanDriverType driverType, bool onlyDrivers)
        {
            var edges = GetEdges(driverType, Dependencies);
            return onlyDrivers ? edges.Intersect(ValidDriverTypes).ToList() : edges;
        }

        public IReadOnlyList<CatalogScanDriverType> GetDependents(CatalogScanDriverType driverType)
        {
            return GetEdges(driverType, Dependents);
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
            if (driverType == Catalog)
            {
                return (CatalogCursorName, await _remoteCursorClient.GetCatalogAsync());
            }
            else if (driverType == FlatContainer)
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
