// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Protocol;

#nullable enable

namespace NuGet.Insights
{
    public class PackageReadmeService
    {
        private readonly PackageWideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpSource _httpSource;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILogger<PackageReadmeService> _logger;

        public PackageReadmeService(
            PackageWideEntityService wideEntityService,
            FlatContainerClient flatContainerClient,
            HttpSource httpSource,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsSettings> options,
            ILogger<PackageReadmeService> logger)
        {
            _wideEntityService = wideEntityService;
            _flatContainerClient = flatContainerClient;
            _httpSource = httpSource;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _wideEntityService.InitializeAsync(_options.Value.PackageReadmeTableName);
        }

        public async Task<IReadOnlyDictionary<ICatalogLeafItem, PackageReadmeInfoV1>> UpdateBatchAsync(string id, IReadOnlyCollection<ICatalogLeafItem> leafItems)
        {
            return await _wideEntityService.UpdateBatchAsync(
                _options.Value.PackageReadmeTableName,
                id,
                leafItems,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        public async Task<PackageReadmeInfoV1> GetOrUpdateInfoAsync(ICatalogLeafItem leafItem)
        {
            return await _wideEntityService.GetOrUpdateInfoAsync(
                _options.Value.PackageReadmeTableName,
                leafItem,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        private async Task<PackageReadmeInfoV1> GetInfoAsync(ICatalogLeafItem leafItem)
        {
            if (leafItem.Type == CatalogLeafType.PackageDelete)
            {
                return MakeUnavailableInfo(leafItem);
            }

            var embeddedUrl = await _flatContainerClient.GetPackageReadmeUrlAsync(leafItem.PackageId, leafItem.PackageVersion);

            var urls = new List<(ReadmeType, string)> { (ReadmeType.Embedded, embeddedUrl) };

            if (_options.Value.LegacyReadmeUrlPattern != null)
            {
                var lowerId = leafItem.PackageId.ToLowerInvariant();
                var lowerVersion = leafItem.ParsePackageVersion().ToNormalizedString().ToLowerInvariant();
                var legacyUrl = string.Format(_options.Value.LegacyReadmeUrlPattern, lowerId, lowerVersion);

                urls.Add((ReadmeType.Legacy, legacyUrl));
            }

            var metric = _telemetryClient.GetMetric($"{nameof(PackageReadmeService)}.{nameof(GetInfoAsync)}.DurationMs");
            var sw = Stopwatch.StartNew();
            var nuGetLog = _logger.ToNuGetLogger();

            try
            {
                foreach ((var readmeType, var url) in urls)
                {
                    var result = await _httpSource.ProcessResponseAsync(
                        new HttpSourceRequest(() => HttpRequestMessageFactory.Create(HttpMethod.Get, url, nuGetLog))
                        {
                            IgnoreNotFounds = true
                        },
                        async response =>
                        {
                            if (response.StatusCode == HttpStatusCode.NotFound)
                            {
                                return null;
                            }

                            response.EnsureSuccessStatusCode();

                            using var destStream = new MemoryStream();
                            using var responseStream = await response.Content.ReadAsStreamAsync();
                            await responseStream.CopyToAsync(destStream);

                            var headers = Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()
                                .Concat(response.Headers)
                                .Concat(response.Content.Headers)
                                .SelectMany(x => x.Value.Select(y => new { x.Key, Value = y }))
                                .ToLookup(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

                            return new PackageReadmeInfoV1
                            {
                                CommitTimestamp = leafItem.CommitTimestamp,
                                ReadmeType = readmeType,
                                HttpHeaders = headers,
                                ReadmeBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
                            };
                        },
                        nuGetLog,
                        token: CancellationToken.None);

                    if (result is not null)
                    {
                        return result;
                    }
                }

                return MakeUnavailableInfo(leafItem);
            }
            finally
            {
                metric.TrackValue(sw.ElapsedMilliseconds);
            }
        }

        private static PackageReadmeInfoV1 MakeUnavailableInfo(ICatalogLeafItem leafItem)
        {
            return new PackageReadmeInfoV1
            {
                CommitTimestamp = leafItem.CommitTimestamp,
                ReadmeType = ReadmeType.None,
            };
        }

        private static PackageReadmeInfoV1 DataToOutput(PackageReadmeInfoVersions data)
        {
            return data.V1;
        }

        private static PackageReadmeInfoVersions OutputToData(PackageReadmeInfoV1 output)
        {
            return new PackageReadmeInfoVersions(output);
        }

        [MessagePackObject]
        public class PackageReadmeInfoVersions : PackageWideEntityService.IPackageWideEntity
        {
            [SerializationConstructor]
            public PackageReadmeInfoVersions(PackageReadmeInfoV1 v1)
            {
                V1 = v1;
            }

            [Key(0)]
            public PackageReadmeInfoV1 V1 { get; set; }

            DateTimeOffset? PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
        }

        [MessagePackObject]
        public class PackageReadmeInfoV1
        {
            [Key(0)]
            public DateTimeOffset CommitTimestamp { get; set; }

            [Key(1)]
            public ReadmeType ReadmeType { get; set; }

            [Key(2)]
            public ILookup<string, string>? HttpHeaders { get; set; }

            [Key(3)]
            public Memory<byte> ReadmeBytes { get; set; }
        }
    }
}
