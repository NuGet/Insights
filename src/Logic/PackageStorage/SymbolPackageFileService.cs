// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using CommunityToolkit.HighPerformance;
using Knapcode.MiniZip;
using MessagePack;

#nullable enable

namespace NuGet.Insights
{
    public class SymbolPackageFileService
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly SymbolPackageClient _symbolPackageClient;
        private readonly PackageWideEntityService _wideEntityService;
        private readonly FileDownloader _fileDownloader;
        private readonly MZipFormat _mzipFormat;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public SymbolPackageFileService(
            SymbolPackageClient symbolPackageClient,
            PackageWideEntityService wideEntityService,
            FileDownloader fileDownloader,
            MZipFormat mzipFormat,
            IOptions<NuGetInsightsSettings> options)
        {
            _initializationState = ContainerInitializationState.New(InitializeInternalAsync, DestroyInternalAsync);
            _symbolPackageClient = symbolPackageClient;
            _wideEntityService = wideEntityService;
            _fileDownloader = fileDownloader;
            _mzipFormat = mzipFormat;
            _options = options;
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
            await _wideEntityService.InitializeAsync(_options.Value.SymbolPackageArchiveTableName);
        }

        private async Task DestroyInternalAsync()
        {
            await _wideEntityService.DeleteTableAsync(_options.Value.SymbolPackageArchiveTableName);
        }

        public async Task<(ZipDirectory Directory, long Length, ILookup<string, string> Headers)?> GetZipDirectoryAndLengthAsync(IPackageIdentityCommit leafItem)
        {
            var info = await GetOrUpdateInfoFromLeafItemAsync(leafItem);
            if (!info.Available)
            {
                return null;
            }

            using var srcStream = info.MZipBytes.AsStream();
            using var destStream = await _mzipFormat.ReadAsync(srcStream);
            var reader = new ZipDirectoryReader(destStream);
            return (await reader.ReadAsync(), destStream.Length, info.HttpHeaders!);
        }

        public async Task<IReadOnlyDictionary<IPackageIdentityCommit, SymbolPackageFileInfoV1>> UpdateBatchFromLeafItemsAsync(
            string id,
            IReadOnlyCollection<IPackageIdentityCommit> leafItems)
        {
            return await _wideEntityService.UpdateBatchAsync(
                _options.Value.SymbolPackageArchiveTableName,
                id,
                leafItems,
                GetInfoFromLeafItemAsync,
                OutputToData,
                DataToOutput);
        }

        public async Task<SymbolPackageFileInfoV1> GetOrUpdateInfoFromLeafItemAsync(IPackageIdentityCommit leafItem)
        {
            return await _wideEntityService.GetOrUpdateInfoAsync(
                _options.Value.SymbolPackageArchiveTableName,
                leafItem,
                GetInfoFromLeafItemAsync,
                OutputToData,
                DataToOutput);
        }

        private async Task<SymbolPackageFileInfoV1> GetInfoFromLeafItemAsync(IPackageIdentityCommit leafItem)
        {
            if (leafItem.LeafType == CatalogLeafType.PackageDelete)
            {
                return MakeDeletedInfo(leafItem);
            }

            return await GetInfoAsync(leafItem);
        }

        private async Task<SymbolPackageFileInfoV1> GetInfoAsync(IPackageIdentityCommit item)
        {
            var url = _symbolPackageClient.GetSymbolPackageUrl(item.PackageId, item.PackageVersion);

            using var reader = await _fileDownloader.GetZipDirectoryReaderAsync(
                item.PackageId,
                item.PackageVersion,
                ArtifactFileType.Snupkg,
                url);

            if (reader is null)
            {
                return MakeDeletedInfo(item);
            }

            return await GetInfoAsync(item, reader.Properties, reader);
        }

        private async Task<SymbolPackageFileInfoV1> GetInfoAsync(
            IPackageIdentityCommit item,
            ILookup<string, string> headers,
            ZipDirectoryReader reader)
        {
            using var destStream = new MemoryStream();
            await _mzipFormat.WriteAsync(reader.Stream, destStream);
            return new SymbolPackageFileInfoV1
            {
                CommitTimestamp = item.CommitTimestamp,
                Available = true,
                HttpHeaders = headers,
                MZipBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
            };
        }

        private static SymbolPackageFileInfoV1 MakeDeletedInfo(IPackageIdentityCommit item)
        {
            return new SymbolPackageFileInfoV1
            {
                CommitTimestamp = item.CommitTimestamp,
                Available = false,
            };
        }

        private static SymbolPackageFileInfoV1 DataToOutput(SymbolPackageFileInfoVersions data)
        {
            return data.V1;
        }

        private static SymbolPackageFileInfoVersions OutputToData(SymbolPackageFileInfoV1 output)
        {
            return new SymbolPackageFileInfoVersions(output);
        }

        [MessagePackObject]
        public class SymbolPackageFileInfoVersions : PackageWideEntityService.IPackageWideEntity
        {
            [SerializationConstructor]
            public SymbolPackageFileInfoVersions(SymbolPackageFileInfoV1 v1)
            {
                V1 = v1;
            }

            [Key(0)]
            public SymbolPackageFileInfoV1 V1 { get; set; }

            DateTimeOffset? PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
        }

        [MessagePackObject]
        public class SymbolPackageFileInfoV1
        {
            [Key(1)]
            public DateTimeOffset? CommitTimestamp { get; set; }

            [Key(2)]
            public bool Available { get; set; }

            [Key(3)]
            public ILookup<string, string>? HttpHeaders { get; set; }

            [Key(4)]
            public Memory<byte> MZipBytes { get; set; }
        }
    }
}
