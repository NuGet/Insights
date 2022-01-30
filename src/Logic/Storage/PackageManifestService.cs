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
using Microsoft.Toolkit.HighPerformance;
using NuGet.Packaging;
using NuGet.Protocol;

#nullable enable

namespace NuGet.Insights
{
    public class PackageManifestService
    {
        private readonly PackageWideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpSource _httpSource;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILogger<PackageManifestService> _logger;

        public PackageManifestService(
            PackageWideEntityService wideEntityService,
            FlatContainerClient flatContainerClient,
            HttpSource httpSource,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsSettings> options,
            ILogger<PackageManifestService> logger)
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
            await _wideEntityService.InitializeAsync(_options.Value.PackageManifestTableName);
        }

        public async Task<(NuspecReader NuspecReader, int ManifestLength)?> GetNuspecReaderAndSizeAsync(ICatalogLeafItem leafItem)
        {
            var result = await GetBytesAndNuspecReaderAsync(leafItem);
            if (result == null)
            {
                return null;
            }

            return (result.Value.NuspecReader, result.Value.ManifestBytes.Length);
        }

        public async Task<(Memory<byte> ManifestBytes, NuspecReader NuspecReader)?> GetBytesAndNuspecReaderAsync(ICatalogLeafItem leafItem)
        {
            var info = await GetOrUpdateInfoAsync(leafItem);
            if (!info.Available)
            {
                return null;
            }

            return (info.ManifestBytes, new NuspecReader(XmlUtility.LoadXml(info.ManifestBytes.AsStream())));
        }

        public async Task<IReadOnlyDictionary<ICatalogLeafItem, PackageManifestInfoV1>> UpdateBatchAsync(string id, IReadOnlyCollection<ICatalogLeafItem> leafItems)
        {
            return await _wideEntityService.UpdateBatchAsync(
                _options.Value.PackageManifestTableName,
                id,
                leafItems,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        public async Task<PackageManifestInfoV1> GetOrUpdateInfoAsync(ICatalogLeafItem leafItem)
        {
            return await _wideEntityService.GetOrUpdateInfoAsync(
                _options.Value.PackageManifestTableName,
                leafItem,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        private async Task<PackageManifestInfoV1> GetInfoAsync(ICatalogLeafItem leafItem)
        {
            if (leafItem.Type == CatalogLeafType.PackageDelete)
            {
                return MakeDeletedInfo(leafItem);
            }

            var url = await _flatContainerClient.GetPackageManifestUrlAsync(leafItem.PackageId, leafItem.PackageVersion);
            var metric = _telemetryClient.GetMetric($"{nameof(PackageManifestService)}.{nameof(GetInfoAsync)}.DurationMs");
            var sw = Stopwatch.StartNew();
            var nuGetLog = _logger.ToNuGetLogger();

            try
            {
                return await _httpSource.ProcessResponseAsync(
                    new HttpSourceRequest(() => HttpRequestMessageFactory.Create(HttpMethod.Get, url, nuGetLog))
                    {
                        IgnoreNotFounds = true
                    },
                    async response =>
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            return MakeDeletedInfo(leafItem);
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

                        return new PackageManifestInfoV1
                        {
                            CommitTimestamp = leafItem.CommitTimestamp,
                            Available = true,
                            HttpHeaders = headers,
                            ManifestBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
                        };
                    },
                    nuGetLog,
                    token: CancellationToken.None);
            }
            finally
            {
                metric.TrackValue(sw.ElapsedMilliseconds);
            }
        }

        private static PackageManifestInfoV1 MakeDeletedInfo(ICatalogLeafItem leafItem)
        {
            return new PackageManifestInfoV1
            {
                CommitTimestamp = leafItem.CommitTimestamp,
                Available = false,
            };
        }

        private static PackageManifestInfoV1 DataToOutput(PackageManifestInfoVersions data)
        {
            return data.V1;
        }

        private static PackageManifestInfoVersions OutputToData(PackageManifestInfoV1 output)
        {
            return new PackageManifestInfoVersions(output);
        }

        [MessagePackObject]
        public class PackageManifestInfoVersions : PackageWideEntityService.IPackageWideEntity
        {
            [SerializationConstructor]
            public PackageManifestInfoVersions(PackageManifestInfoV1 v1)
            {
                V1 = v1;
            }

            [Key(0)]
            public PackageManifestInfoV1 V1 { get; set; }

            DateTimeOffset PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
        }

        [MessagePackObject]
        public class PackageManifestInfoV1
        {
            [Key(1)]
            public DateTimeOffset CommitTimestamp { get; set; }

            [Key(2)]
            public bool Available { get; set; }

            [Key(3)]
            public ILookup<string, string>? HttpHeaders { get; set; }

            [Key(4)]
            public Memory<byte> ManifestBytes { get; set; }
        }
    }
}
