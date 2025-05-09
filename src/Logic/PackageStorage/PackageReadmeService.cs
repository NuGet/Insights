// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;

#nullable enable

namespace NuGet.Insights
{
    public class PackageReadmeService
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly PackageWideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly Func<HttpClient> _httpClientFactory;
        private readonly ExternalBlobStorageClient _storageClient;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILogger<PackageReadmeService> _logger;

        public PackageReadmeService(
            PackageWideEntityService wideEntityService,
            FlatContainerClient flatContainerClient,
            Func<HttpClient> httpClientFactory,
            ExternalBlobStorageClient storageClient,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsSettings> options,
            ILogger<PackageReadmeService> logger)
        {
            _initializationState = ContainerInitializationState.New(InitializeInternalAsync, DestroyInternalAsync);
            _wideEntityService = wideEntityService;
            _flatContainerClient = flatContainerClient;
            _httpClientFactory = httpClientFactory;
            _storageClient = storageClient;
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

        private async Task InitializeInternalAsync()
        {
            await _wideEntityService.InitializeAsync(_options.Value.PackageReadmeTableName);
        }

        private async Task DestroyInternalAsync()
        {
            await _wideEntityService.DeleteTableAsync(_options.Value.PackageReadmeTableName);
        }

        public async Task<IReadOnlyDictionary<IPackageIdentityCommit, PackageReadmeInfoV1>> UpdateBatchFromLeafItemsAsync(
            string id,
            IReadOnlyCollection<IPackageIdentityCommit> leafItems)
        {
            return await _wideEntityService.UpdateBatchAsync(
                _options.Value.PackageReadmeTableName,
                id,
                leafItems,
                GetInfoFromLeafItemAsync,
                OutputToData,
                DataToOutput);
        }

        public async Task<PackageReadmeInfoV1> GetOrUpdateInfoFromLeafItemAsync(IPackageIdentityCommit leafItem)
        {
            return await _wideEntityService.GetOrUpdateInfoAsync(
                _options.Value.PackageReadmeTableName,
                leafItem,
                GetInfoFromLeafItemAsync,
                OutputToData,
                DataToOutput);
        }

        private async Task<PackageReadmeInfoV1> GetInfoFromLeafItemAsync(IPackageIdentityCommit leafItem)
        {
            if (leafItem.LeafType == CatalogLeafType.PackageDelete)
            {
                return MakeUnavailableInfo(leafItem);
            }

            return await GetInfoAsync(leafItem);
        }

        private async Task<PackageReadmeInfoV1> GetInfoAsync(IPackageIdentityCommit item)
        {
            var embeddedUrl = await _flatContainerClient.GetPackageReadmeUrlAsync(item.PackageId, item.PackageVersion);

            var urls = new List<(ReadmeType Type, string Url, bool UseStorageClient)> { (ReadmeType.Embedded, embeddedUrl, false) };

            if (_options.Value.LegacyReadmeUrlPattern != null)
            {
                var lowerId = item.PackageId.ToLowerInvariant();
                var lowerVersion = NuGetVersion.Parse(item.PackageVersion).ToNormalizedString().ToLowerInvariant();
                var legacyUrl = string.Format(CultureInfo.InvariantCulture, _options.Value.LegacyReadmeUrlPattern, lowerId, lowerVersion);

                urls.Add((ReadmeType.Legacy, legacyUrl, true));
            }

            var metric = _telemetryClient.GetMetric($"{nameof(PackageReadmeService)}.{nameof(GetInfoAsync)}.DurationMs");
            var sw = Stopwatch.StartNew();
            try
            {
                foreach ((var readmeType, var url, var useStorageClient) in urls)
                {
                    if (useStorageClient)
                    {
                        var result = await GetPackageReadmeInfoWithStorageClientAsync(item, readmeType, url);
                        if (result is not null)
                        {
                            return result;
                        }
                    }
                    else
                    {
                        var result = await GetPackageReadmeInfoWithHttpClientAsync(item, readmeType, url);
                        if (result is not null)
                        {
                            return result;
                        }
                    }
                }

                return MakeUnavailableInfo(item);
            }
            finally
            {
                metric.TrackValue(sw.ElapsedMilliseconds);
            }
        }

        private async Task<PackageReadmeInfoV1?> GetPackageReadmeInfoWithStorageClientAsync(IPackageIdentityCommit item, ReadmeType readmeType, string url)
        {
            using var response = await _storageClient.GetResponseAsync(new ExternalBlobRequest(
                 BlobRequestMethod.Get,
                 new Uri(url),
                 UseThrottle: false,
                 AllowNotFound: true));
            if (response is null)
            {
                return null;
            }

            using var destStream = new MemoryStream();
            using var responseStream = response.Stream!;
            await responseStream.CopyToAsync(destStream);

            return new PackageReadmeInfoV1
            {
                CommitTimestamp = item.CommitTimestamp,
                ReadmeType = readmeType,
                HttpHeaders = response.Headers,
                ReadmeBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
            };
        }

        private async Task<PackageReadmeInfoV1?> GetPackageReadmeInfoWithHttpClientAsync(IPackageIdentityCommit item, ReadmeType readmeType, string url)
        {
            var httpClient = _httpClientFactory();
            return await httpClient.ProcessResponseWithRetriesAsync(
                () => new HttpRequestMessage(HttpMethod.Get, url),
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

                    return new PackageReadmeInfoV1
                    {
                        CommitTimestamp = item.CommitTimestamp,
                        ReadmeType = readmeType,
                        HttpHeaders = response.GetHeaderLookup(),
                        ReadmeBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
                    };
                },
                _logger,
                token: CancellationToken.None);
        }

        private static PackageReadmeInfoV1 MakeUnavailableInfo(IPackageIdentityCommit item)
        {
            return new PackageReadmeInfoV1
            {
                CommitTimestamp = item.CommitTimestamp,
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
            public DateTimeOffset? CommitTimestamp { get; set; }

            [Key(1)]
            public ReadmeType ReadmeType { get; set; }

            [Key(2)]
            public ILookup<string, string>? HttpHeaders { get; set; }

            [Key(3)]
            public Memory<byte> ReadmeBytes { get; set; }
        }
    }
}
