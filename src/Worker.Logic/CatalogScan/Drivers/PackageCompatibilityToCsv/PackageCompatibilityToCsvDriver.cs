// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Frameworks;
using NuGetGallery;

namespace NuGet.Insights.Worker.PackageCompatibilityToCsv
{
    public class PackageCompatibilityToCsvDriver : ICatalogLeafToCsvDriver<PackageCompatibility>, ICsvResultStorage<PackageCompatibility>
    {
        private static readonly NuGetFrameworkSorter Sorter = new NuGetFrameworkSorter();

        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly PackageManifestService _packageManifestService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<PackageCompatibilityToCsvDriver> _logger;

        public PackageCompatibilityToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            PackageManifestService packageManifestService,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<PackageCompatibilityToCsvDriver> logger)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _packageManifestService = packageManifestService;
            _options = options;
            _logger = logger;
        }

        public bool SingleMessagePerId => false;
        public string ResultContainerName => _options.Value.PackageCompatibilityContainerName;

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
            await _packageManifestService.InitializeAsync();
        }

        public async Task<DriverResult<CsvRecordSet<PackageCompatibility>>> ProcessLeafAsync(ICatalogLeafItem item, int attemptCount)
        {
            var records = await ProcessLeafInternalAsync(item);
            return DriverResult.Success(new CsvRecordSet<PackageCompatibility>(PackageRecord.GetBucketKey(item), records));
        }

        private async Task<List<PackageCompatibility>> ProcessLeafInternalAsync(ICatalogLeafItem item)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (item.Type == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                return new List<PackageCompatibility> { new PackageCompatibility(scanId, scanTimestamp, leaf) };
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                var zipDirectory = await _packageFileService.GetZipDirectoryAsync(item);
                if (zipDirectory == null)
                {
                    // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted record.
                    return new List<PackageCompatibility>();
                }

                (var manifestBytes, var nuspecReader) = await _packageManifestService.GetBytesAndNuspecReaderAsync(item);
                if (nuspecReader == null)
                {
                    // Ignore packages where the .nuspec is missing. A subsequent scan will produce a deleted record.
                    return new List<PackageCompatibility>();
                }

                var escapedFiles = zipDirectory
                    .Entries
                    .Select(x => x.GetName())
                    .ToList();
                var files = escapedFiles
                    .Select(x => x.IndexOf('%', StringComparison.Ordinal) >= 0 ? Uri.UnescapeDataString(x) : x)
                    .ToList();

                var output = new PackageCompatibility(scanId, scanTimestamp, leaf);
                var hasError = false;
                var doesNotRoundTrip = false;
                var hasAny = false;
                var hasUnsupported = false;
                var hasAgnostic = false;
                var brokenFrameworks = new HashSet<string>();
                string GetAndSerializeNested(string methodName, Func<IEnumerable<NuGetFramework>> getFrameworks)
                {
                    return GetAndSerialize(
                        item,
                        ref hasError,
                        ref doesNotRoundTrip,
                        ref hasAny,
                        ref hasUnsupported,
                        ref hasAgnostic,
                        brokenFrameworks,
                        methodName,
                        getFrameworks);
                }

                output.NuspecReader = GetAndSerializeNested(
                    nameof(output.NuspecReader),
                    () =>
                    {
                        var packageReader = new InMemoryPackageReader(manifestBytes, escapedFiles);
                        return packageReader.GetSupportedFrameworks().ToList();
                    });

                output.NuGetGallery = GetAndSerializeNested(
                    nameof(output.NuGetGallery),
                    () =>
                    {
                        var packageService = new PackageService();
                        return packageService.GetSupportedFrameworks(nuspecReader, files);
                    });

                output.NuGetGalleryEscaped = GetAndSerializeNested(
                    nameof(output.NuGetGallery),
                    () =>
                    {
                        var packageService = new PackageService();
                        return packageService.GetSupportedFrameworks(nuspecReader, escapedFiles);
                    });

                var nuGetLogger = _logger.ToNuGetLogger();
                output.NU1202 = GetAndSerializeNested(
                    nameof(output.NU1202),
                    () => CompatibilityChecker.GetPackageFrameworks(files, nuGetLogger));

                output.HasError = hasError;
                output.DoesNotRoundTrip = doesNotRoundTrip;
                output.HasAny = hasAny;
                output.HasUnsupported = hasUnsupported;
                output.HasAgnostic = hasAgnostic;
                output.BrokenFrameworks = JsonSerializer.Serialize(brokenFrameworks.OrderBy(x => x).ToList());

                return new List<PackageCompatibility> { output };
            }
        }

        private string GetAndSerialize(
            ICatalogLeafItem item,
            ref bool hasError,
            ref bool doesNotRoundTrip,
            ref bool hasAny,
            ref bool hasUnsupported,
            ref bool hasAgnostic,
            HashSet<string> brokenFrameworks,
            string methodName,
            Func<IEnumerable<NuGetFramework>> getFrameworks)
        {
            List<string> roundTripShortFolderNames;
            try
            {
                var originalFrameworks = getFrameworks()
                    .OrderBy(x => x, Sorter)
                    .ToList();
                var originalShortFolderNames = originalFrameworks
                    .Select(x => x.GetShortFolderName())
                    .ToList();
                var roundTripFrameworks = originalShortFolderNames
                    .Select(x => NuGetFramework.Parse(x))
                    .OrderBy(x => x, Sorter)
                    .ToList();
                roundTripShortFolderNames = roundTripFrameworks
                    .Select(x => x.GetShortFolderName())
                    .ToList();

                hasAny |= roundTripFrameworks.Any(x => x.IsAny);
                hasUnsupported |= roundTripFrameworks.Any(x => x.IsUnsupported);
                hasAgnostic |= roundTripFrameworks.Any(x => x.IsAgnostic);

                if (!originalFrameworks.SequenceEqual(roundTripFrameworks)
                    || !originalShortFolderNames.SequenceEqual(roundTripShortFolderNames))
                {
                    doesNotRoundTrip = true;

                    foreach (var framework in originalFrameworks.Except(roundTripFrameworks))
                    {
                        // Use the full string to capture any weird values. The short folder name loses information on some bad data.
                        brokenFrameworks.Add(framework.ToString());
                    }

                    foreach (var framework in originalShortFolderNames.Except(roundTripShortFolderNames))
                    {
                        brokenFrameworks.Add(framework);
                    }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger.LogWarning(ex, "For {Id}/{Version}, failed to determine compatible frameworks using '{MethodName}'.", item.PackageId, item.PackageVersion, methodName);
                hasError = true;
                return null;
            }

            return JsonSerializer.Serialize(roundTripShortFolderNames);
        }

        public List<PackageCompatibility> Prune(List<PackageCompatibility> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public Task<ICatalogLeafItem> MakeReprocessItemOrNullAsync(PackageCompatibility record)
        {
            throw new NotImplementedException();
        }
    }
}
