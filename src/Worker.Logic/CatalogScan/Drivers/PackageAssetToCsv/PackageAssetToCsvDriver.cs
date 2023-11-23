// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

namespace NuGet.Insights.Worker.PackageAssetToCsv
{
    public class PackageAssetToCsvDriver : ICatalogLeafToCsvDriver<PackageAsset>, ICsvResultStorage<PackageAsset>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<PackageAssetToCsvDriver> _logger;

        private static readonly IReadOnlyDictionary<string, PatternSetType> NameToPatternSetType = Enum
            .GetValues(typeof(PatternSetType))
            .Cast<PatternSetType>()
            .ToDictionary(x => x.ToString(), x => x);

        public PackageAssetToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<PackageAssetToCsvDriver> logger)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _options = options;
            _logger = logger;
        }

        public string ResultContainerName => _options.Value.PackageAssetContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageAsset> Prune(List<PackageAsset> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<CsvRecordSet<PackageAsset>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var records = await ProcessLeafInternalAsync(leafScan);
            return DriverResult.Success(new CsvRecordSet<PackageAsset>(PackageRecord.GetBucketKey(leafScan), records));
        }

        private async Task<List<PackageAsset>> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);
                return new List<PackageAsset> { new PackageAsset(scanId, scanTimestamp, leaf) };
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var zipDirectory = await _packageFileService.GetZipDirectoryAsync(leafScan.ToPackageIdentityCommit());
                if (zipDirectory == null)
                {
                    // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted asset record.
                    return new List<PackageAsset>();
                }

                var files = zipDirectory
                    .Entries
                    .Select(x => x.GetName())
                    .ToList();

                return GetAssets(scanId, scanTimestamp, leaf, files);
            }
        }

        private List<PackageAsset> GetAssets(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, IReadOnlyList<string> files)
        {
            var contentItemCollection = new ContentItemCollection();
            contentItemCollection.Load(files);

            var assets = new List<PackageAsset>();
            foreach (var pair in GetPatternSets())
            {
                var groups = new List<ContentItemGroup>();
                try
                {
                    // We must enumerate the item groups here to force the exception to be thrown, if any.
                    contentItemCollection.PopulateItemGroups(pair.Value, groups);
                }
                catch (ArgumentException ex) when (IsInvalidDueToHyphenInProfile(ex))
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
                    var roundTripTargetFrameworkMoniker = parsedFramework.GetShortFolderName();

                    foreach (var item in group.Items)
                    {
                        assets.Add(new PackageAsset(scanId, scanTimestamp, leaf, PackageAssetResultType.AvailableAssets)
                        {
                            PatternSet = NameToPatternSetType[pair.Key],

                            PropertyAnyValue = (string)anyValue,
                            PropertyCodeLanguage = (string)codeLanguage,
                            PropertyTargetFrameworkMoniker = targetFrameworkMoniker,
                            PropertyManagedAssembly = (string)managedAssembly,
                            PropertyMSBuild = (string)msbuild,
                            PropertyRuntimeIdentifier = (string)runtimeIdentifier,
                            PropertySatelliteAssembly = (string)satelliteAssembly,

                            Path = item.Path,
                            FileName = Path.GetFileName(item.Path),
                            FileExtension = Path.GetExtension(item.Path),
                            TopLevelFolder = PathUtility.GetTopLevelFolder(item.Path),

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

        internal static Dictionary<string, PatternSet> GetPatternSets()
        {
            var runtimeGraph = new RuntimeGraph();
            var conventions = new ManagedCodeConventions(runtimeGraph);

            return conventions
                .Patterns
                .GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty)
                .Where(x => x.Name != nameof(ManagedCodeConventions.Patterns.AnyTargettedFile)) // This pattern is unused.
                .ToDictionary(x => x.Name, x => (PatternSet)x.GetGetMethod().Invoke(conventions.Patterns, null));
        }

        private List<PackageAsset> GetErrorResult(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, Exception ex, string message)
        {
            _logger.LogWarning(ex, message, leaf.PackageId, leaf.PackageVersion);
            return new List<PackageAsset> { new PackageAsset(scanId, scanTimestamp, leaf, PackageAssetResultType.Error) };
        }

        private static bool IsInvalidDueMissingPortableProfile(FrameworkException ex)
        {
            return
                ex.Message.StartsWith("Invalid portable frameworks for '", StringComparison.Ordinal)
                && ex.Message.EndsWith("'. A portable framework must have at least one framework in the profile.", StringComparison.Ordinal);
        }

        public static bool IsInvalidDueToHyphenInProfile(ArgumentException ex)
        {
            return
                ex.Message.StartsWith("Invalid portable frameworks '", StringComparison.Ordinal)
                && ex.Message.EndsWith("'. A hyphen may not be in any of the portable framework names.", StringComparison.Ordinal);
        }
    }
}
