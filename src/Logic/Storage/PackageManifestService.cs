using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Toolkit.HighPerformance.Extensions;
using NuGet.Packaging;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages
{
    public class PackageManifestService
    {
        private readonly PackageWideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpSource _httpSource;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<ExplorePackagesSettings> _options;
        private readonly ILogger<PackageManifestService> _logger;

        public PackageManifestService(
            PackageWideEntityService wideEntityService,
            FlatContainerClient flatContainerClient,
            HttpSource httpSource,
            ITelemetryClient telemetryClient,
            IOptions<ExplorePackagesSettings> options,
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

        public async Task<NuspecReader> GetNuspecReaderAsync(CatalogLeafItem leafItem)
        {
            var manifest = await GetManifestAsync(leafItem);
            if (manifest == null)
            {
                return null;
            }

            return new NuspecReader(manifest);
        }

        public async Task<XDocument> GetManifestAsync(CatalogLeafItem leafItem)
        {
            var info = await GetOrUpdateInfoAsync(leafItem);
            if (!info.Available)
            {
                return null;
            }

            return XmlUtility.LoadXml(info.ManifestBytes.AsStream());
        }

        public async Task<IReadOnlyDictionary<CatalogLeafItem, PackageManifestInfoV1>> UpdateBatchAsync(string id, IReadOnlyCollection<CatalogLeafItem> leafItems)
        {
            return await _wideEntityService.UpdateBatchAsync(
                _options.Value.PackageManifestTableName,
                id,
                leafItems,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        public async Task<PackageManifestInfoV1> GetOrUpdateInfoAsync(CatalogLeafItem leafItem)
        {
            return await _wideEntityService.GetOrUpdateInfoAsync(
                _options.Value.PackageManifestTableName,
                leafItem,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        private async Task<PackageManifestInfoV1> GetInfoAsync(CatalogLeafItem leafItem)
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

        private static PackageManifestInfoV1 MakeDeletedInfo(CatalogLeafItem leafItem)
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
            return new PackageManifestInfoVersions { V1 = output };
        }

        [MessagePackObject]
        public class PackageManifestInfoVersions : PackageWideEntityService.IPackageWideEntity
        {
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
            public ILookup<string, string> HttpHeaders { get; set; }

            [Key(4)]
            public Memory<byte> ManifestBytes { get; set; }
        }
    }
}
