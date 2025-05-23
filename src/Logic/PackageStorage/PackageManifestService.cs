// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using CommunityToolkit.HighPerformance;
using MessagePack;
using NuGet.Packaging;

#nullable enable

namespace NuGet.Insights
{
    public class PackageManifestService
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly PackageWideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly Func<HttpClient> _httpClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILogger<PackageManifestService> _logger;

        public PackageManifestService(
            PackageWideEntityService wideEntityService,
            FlatContainerClient flatContainerClient,
            Func<HttpClient> httpClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsSettings> options,
            ILogger<PackageManifestService> logger)
        {
            _initializationState = ContainerInitializationState.New(InitializeInternalAsync, DestroyInternalAsync);
            _wideEntityService = wideEntityService;
            _flatContainerClient = flatContainerClient;
            _httpClientFactory = httpClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _initializationState.InitializeAsync();
        }

        public async Task DestroyAsync()
        {
            await _initializationState.DestroyAsync();
        }

        public async Task InitializeInternalAsync()
        {
            await _wideEntityService.InitializeAsync(_options.Value.PackageManifestTableName);
        }

        public async Task DestroyInternalAsync()
        {
            await _wideEntityService.DeleteTableAsync(_options.Value.PackageManifestTableName);
        }

        public async Task<(NuspecReader NuspecReader, int ManifestLength)?> GetNuspecReaderAndSizeAsync(IPackageIdentityCommit leafItem)
        {
            var result = await GetBytesAndNuspecReaderAsync(leafItem);
            if (result == null)
            {
                return null;
            }

            return (result.Value.NuspecReader, result.Value.ManifestBytes.Length);
        }

        public async Task<(Memory<byte> ManifestBytes, NuspecReader NuspecReader)?> GetBytesAndNuspecReaderAsync(IPackageIdentityCommit leafItem)
        {
            var info = await GetOrUpdateInfoAsync(leafItem);
            if (!info.Available)
            {
                return null;
            }

            return (info.ManifestBytes, new NuspecReader(XmlUtility.LoadXml(info.ManifestBytes.AsStream())));
        }

        public async Task<IReadOnlyDictionary<IPackageIdentityCommit, PackageManifestInfoV1>> UpdateBatchAsync(string id, IReadOnlyCollection<IPackageIdentityCommit> leafItems)
        {
            return await _wideEntityService.UpdateBatchAsync(
                _options.Value.PackageManifestTableName,
                id,
                leafItems,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        public async Task<PackageManifestInfoV1> GetOrUpdateInfoAsync(IPackageIdentityCommit leafItem)
        {
            return await _wideEntityService.GetOrUpdateInfoAsync(
                _options.Value.PackageManifestTableName,
                leafItem,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        private async Task<PackageManifestInfoV1> GetInfoAsync(IPackageIdentityCommit leafItem)
        {
            if (leafItem.LeafType == CatalogLeafType.PackageDelete)
            {
                return MakeDeletedInfo(leafItem);
            }

            var url = await _flatContainerClient.GetPackageManifestUrlAsync(leafItem.PackageId, leafItem.PackageVersion);
            var metric = _telemetryClient.GetMetric($"{nameof(PackageManifestService)}.{nameof(GetInfoAsync)}.DurationMs");
            var sw = Stopwatch.StartNew();

            try
            {
                var httpClient = _httpClientFactory();
                return await httpClient.ProcessResponseWithRetriesAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, url),
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

                        return new PackageManifestInfoV1
                        {
                            CommitTimestamp = leafItem.CommitTimestamp,
                            Available = true,
                            HttpHeaders = response.GetHeaderLookup(),
                            ManifestBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
                        };
                    },
                    _logger,
                    token: CancellationToken.None);
            }
            finally
            {
                metric.TrackValue(sw.ElapsedMilliseconds);
            }
        }

        private static PackageManifestInfoV1 MakeDeletedInfo(IPackageIdentityCommit leafItem)
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

            DateTimeOffset? PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
        }

        [MessagePackObject]
        public class PackageManifestInfoV1
        {
            [Key(1)]
            public DateTimeOffset? CommitTimestamp { get; set; }

            [Key(2)]
            public bool Available { get; set; }

            [Key(3)]
            public ILookup<string, string>? HttpHeaders { get; set; }

            [Key(4)]
            public Memory<byte> ManifestBytes { get; set; }
        }
    }
}
