using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic.Worker.BlobStorage;
using Knapcode.MiniZip;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class FindPackageAssetsScanDriver : ICatalogScanDriver
    {
        public static readonly string ContainerName = "findpackageassets";

        private readonly SchemaSerializer _schemaSerializer;
        private readonly CatalogClient _catalogClient;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly AppendResultStorageService _storageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly ILogger<FindPackageAssetsScanDriver> _logger;

        public FindPackageAssetsScanDriver(
            SchemaSerializer schemaSerializer,
            CatalogClient catalogClient,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpZipProvider httpZipProvider,
            AppendResultStorageService storageService,
            TaskStateStorageService taskStateStorageService,
            MessageEnqueuer messageEnqueuer,
            ILogger<FindPackageAssetsScanDriver> logger)
        {
            _schemaSerializer = schemaSerializer;
            _catalogClient = catalogClient;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _httpZipProvider = httpZipProvider;
            _storageService = storageService;
            _taskStateStorageService = taskStateStorageService;
            _messageEnqueuer = messageEnqueuer;
            _logger = logger;
        }

        public async Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan)
        {
            await _storageService.InitializeAsync(ContainerName);
            await _storageService.InitializeAsync(GetContainerName(indexScan.StorageSuffix));
            await _taskStateStorageService.InitializeAsync(indexScan.StorageSuffix);

            return CatalogIndexScanResult.Expand;
        }

        public Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan)
        {
            return Task.FromResult(CatalogPageScanResult.Expand);
        }

        public async Task ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;
            var parameters = GetParameters(leafScan.ScanParameters);

            if (leafScan.ParsedLeafType == CatalogLeafType.PackageDelete)
            {
                // Ignore delete events.
                return;
            }

            var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.ParsedLeafType, leafScan.Url);

            var flatContainerBaseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var normalizedVersion = NuGetVersion.Parse(leafScan.PackageVersion).ToNormalizedString();
            var url = _flatContainerClient.GetPackageContentUrl(flatContainerBaseUrl, leafScan.PackageId, leafScan.PackageVersion);

            List<string> files;
            try
            {
                using (var reader = await _httpZipProvider.GetReaderAsync(new Uri(url)))
                {
                    var zipDirectory = await reader.ReadAsync();
                    files = zipDirectory
                        .Entries
                        .Select(x => x.GetName())
                        .Select(x => x.Replace('\\', '/'))
                        .Distinct()
                        .ToList();
                }
            }
            catch (MiniZipHttpStatusCodeException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignore deleted packages.
                return;
            }

            var assets = GetAssets(scanId, scanTimestamp, leaf, files);
            var storage = new AppendResultStorage(GetContainerName(leafScan.StorageSuffix), parameters.BucketCount);
            var bucketKey = $"{leaf.PackageId}/{leaf.PackageVersion}".ToLowerInvariant();
            await _storageService.AppendAsync(storage, bucketKey, assets);
        }

        private FindPackageAssetsParameters GetParameters(string scanParameters)
        {
            return (FindPackageAssetsParameters)_schemaSerializer.Deserialize(scanParameters);
        }

        private List<PackageAsset> GetAssets(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, List<string> files)
        {
            var contentItemCollection = new ContentItemCollection();
            contentItemCollection.Load(files);

            var runtimeGraph = new RuntimeGraph();
            var conventions = new ManagedCodeConventions(runtimeGraph);

            var patternSets = conventions
                .Patterns
                .GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty)
                .Where(x => x.Name != nameof(ManagedCodeConventions.Patterns.AnyTargettedFile)) // This pattern is unused.
                .ToDictionary(x => x.Name, x => (PatternSet)x.GetGetMethod().Invoke(conventions.Patterns, null));

            var assets = new List<PackageAsset>();
            foreach (var pair in patternSets)
            {
                List<ContentItemGroup> groups;
                try
                {
                    // We must enumerate the item groups here to force the exception to be thrown, if any.
                    groups = contentItemCollection.FindItemGroups(pair.Value).ToList();
                }
                catch (ArgumentException ex) when (IsInvalidDueToHyphenInPortal(ex))
                {
                    return GetErrorResult(scanId, scanTimestamp, leaf, ex, "Package {Id} {Version} contains a portable framework with a hyphen in the profile.");
                }

                foreach (var group in groups)
                {
                    group.Properties.TryGetValue(ManagedCodeConventions.PropertyNames.AnyValue, out var anyValue);
                    group.Properties.TryGetValue(ManagedCodeConventions.PropertyNames.CodeLanguage, out var codeLanguage);
                    group.Properties.TryGetValue(ManagedCodeConventions.PropertyNames.Locale, out var locale);
                    group.Properties.TryGetValue(ManagedCodeConventions.PropertyNames.ManagedAssembly, out var managedAssembly);
                    group.Properties.TryGetValue(ManagedCodeConventions.PropertyNames.MSBuild, out var msbuild);
                    group.Properties.TryGetValue(ManagedCodeConventions.PropertyNames.RuntimeIdentifier, out var runtimeIdentifier);
                    group.Properties.TryGetValue(ManagedCodeConventions.PropertyNames.SatelliteAssembly, out var satelliteAssembly);

                    string targetFrameworkMoniker;
                    try
                    {
                        targetFrameworkMoniker = ((NuGetFramework)group.Properties[ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker]).GetShortFolderName();
                    }
                    catch (FrameworkException ex) when (IsInvalidDueMissingPortableProfile(ex))
                    {
                        return GetErrorResult(scanId, scanTimestamp, leaf, ex, "Package {Id} {Version} contains a portable framework missing a profile.");
                    }

                    var parsedFramework = NuGetFramework.Parse(targetFrameworkMoniker);
                    string roundTripTargetFrameworkMoniker = parsedFramework.GetShortFolderName();

                    foreach (var item in group.Items)
                    {
                        assets.Add(new PackageAsset(scanId, scanTimestamp, leaf, PackageAssetResultType.AvailableAssets)
                        {
                            PatternSet = pair.Key,

                            PropertyAnyValue = (string)anyValue,
                            PropertyCodeLanguage = (string)codeLanguage,
                            PropertyTargetFrameworkMoniker = targetFrameworkMoniker,
                            PropertyManagedAssembly = (string)managedAssembly,
                            PropertyMSBuild = (string)msbuild,
                            PropertyRuntimeIdentifier = (string)runtimeIdentifier,
                            PropertySatelliteAssembly = (string)satelliteAssembly,

                            Path = item.Path,
                            TopLevelFolder = item.Path.Split('/')[0].ToLowerInvariant(),
                            FileName = Path.GetFileName(item.Path),
                            FileExtension = Path.GetExtension(item.Path),

                            RoundTripTargetFrameworkMoniker = roundTripTargetFrameworkMoniker,
                            FrameworkName = parsedFramework.Framework,
                            FrameworkVersion = parsedFramework.Version?.ToString(),
                            FrameworkProfile = parsedFramework.Profile,
                            PlatformName = parsedFramework.Platform,
                            PlatformVersion = parsedFramework.PlatformVersion?.ToString(),
                        });
                    }
                }
            }

            if (assets.Count == 0)
            {
                return new List<PackageAsset> { new PackageAsset(scanId, scanTimestamp, leaf, PackageAssetResultType.NoAssets) };
            }

            return assets;
        }

        private List<PackageAsset> GetErrorResult(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, Exception ex, string message)
        {
            _logger.LogWarning(ex, message, leaf.PackageId, leaf.PackageVersion);
            return new List<PackageAsset> { new PackageAsset(scanId, scanTimestamp, leaf, PackageAssetResultType.Error) };
        }

        private static bool IsInvalidDueMissingPortableProfile(FrameworkException ex)
        {
            return
                ex.Message.StartsWith("Invalid portable frameworks for '")
                && ex.Message.EndsWith("'. A portable framework must have at least one framework in the profile.");
        }

        private static bool IsInvalidDueToHyphenInPortal(ArgumentException ex)
        {
            return
                ex.Message.StartsWith("Invalid portable frameworks '")
                && ex.Message.EndsWith("'. A hyphen may not be in any of the portable framework names.");
        }

        public async Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            var buckets = await _storageService.GetWrittenAppendBuckets(GetContainerName(indexScan.StorageSuffix));

            var partitionKey = GetAggregateTasksPartitionKey(indexScan);

            await _taskStateStorageService.InitializeAllAsync(
                indexScan.StorageSuffix,
                partitionKey,
                buckets.Select(x => x.ToString()).ToList());

            var messages = buckets
                .Select(b => new FindPackageAssetsCompactMessage
                {
                    SourceContainer = GetContainerName(indexScan.StorageSuffix),
                    DestinationContainer = ContainerName,
                    Bucket = b,
                    TaskStateStorageSuffix = indexScan.StorageSuffix,
                    TaskStatePartitionKey = partitionKey,
                    TaskStateRowKey = b.ToString(),
                })
                .ToList();
            await _messageEnqueuer.EnqueueAsync(messages);
        }

        public async Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            var countLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(
                indexScan.StorageSuffix,
                GetAggregateTasksPartitionKey(indexScan));
            _logger.LogInformation("There are at least {Count} compact tasks pending.", countLowerBound);
            return countLowerBound == 0;
        }

        private static string GetAggregateTasksPartitionKey(CatalogIndexScan indexScan)
        {
            return $"{indexScan.ScanId}-aggregate";
        }

        public async Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            if (!string.IsNullOrEmpty(indexScan.StorageSuffix))
            {
                await _taskStateStorageService.DeleteTableAsync(indexScan.StorageSuffix);
                await _storageService.DeleteAsync(GetContainerName(indexScan.StorageSuffix));
            }
        }

        private static string GetContainerName(string suffix)
        {
            return $"{ContainerName}{suffix}";
        }
    }
}
