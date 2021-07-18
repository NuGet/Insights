// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Frameworks;

namespace NuGet.Insights.Worker.PackageCompatibilityToCsv
{

    public class PackageCompatibilityToCsvDriver : ICatalogLeafToCsvDriver<PackageCompatibility>, ICsvResultStorage<PackageCompatibility>
    {
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

        public async Task<DriverResult<CsvRecordSet<PackageCompatibility>>> ProcessLeafAsync(CatalogLeafItem item, int attemptCount)
        {
            var records = await ProcessLeafInternalAsync(item);
            return DriverResult.Success(new CsvRecordSet<PackageCompatibility>(PackageRecord.GetBucketKey(item), records));
        }

        private async Task<List<PackageCompatibility>> ProcessLeafInternalAsync(CatalogLeafItem item)
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

                var files = zipDirectory
                    .Entries
                    .Select(x => x.GetName())
                    .ToList();

                var output = new PackageCompatibility(scanId, scanTimestamp, leaf);
                var hasError = false;
                var doesNotRoundTrip = false;

                output.NuspecReader = GetAndSerialize(
                    item,
                    ref hasError,
                    ref doesNotRoundTrip,
                    nameof(output.NuspecReader),
                    () =>
                    {
                        var packageReader = new InMemoryPackageReader(files, manifestBytes);
                        return packageReader.GetSupportedFrameworks().ToList();
                    });

                output.NuGetGallery = GetAndSerialize(
                    item,
                    ref hasError,
                    ref doesNotRoundTrip,
                    nameof(output.NuGetGallery),
                    () => NuGetGallery.GetSupportedFrameworks(nuspecReader, files));

                output.HasError = hasError;
                output.DoesNotRoundTrip = doesNotRoundTrip;

                return new List<PackageCompatibility> { output };
            }
        }

        private string GetAndSerialize(CatalogLeafItem item, ref bool hasError, ref bool doesNotRoundTrip, string methodName, Func<IEnumerable<NuGetFramework>> getFrameworks)
        {
            List<string> roundTripFrameworks;
            try
            {
                var frameworks = getFrameworks().ToList();
                var originalFrameworks = frameworks.Select(x => x.GetShortFolderName()).OrderBy(x => x).ToList();
                roundTripFrameworks = originalFrameworks.Select(x => NuGetFramework.Parse(x).GetShortFolderName()).OrderBy(x => x).ToList();
                if (!originalFrameworks.SequenceEqual(roundTripFrameworks))
                {
                    doesNotRoundTrip = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "For {Id}/{Version}, failed to determine compatible frameworks using '{MethodName}'.", item.PackageId, item.PackageVersion, methodName);
                hasError = true;
                return null;
            }

            return JsonConvert.SerializeObject(roundTripFrameworks);
        }

        public List<PackageCompatibility> Prune(List<PackageCompatibility> records)
        {
            return PackageRecord.Prune(records);
        }

        public Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(PackageCompatibility record)
        {
            throw new NotImplementedException();
        }
    }
}
