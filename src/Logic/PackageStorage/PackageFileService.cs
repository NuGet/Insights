// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.Pkcs;
using CommunityToolkit.HighPerformance;
using Knapcode.MiniZip;
using MessagePack;
using NuGet.Packaging.Signing;

#nullable enable

namespace NuGet.Insights
{
    public class PackageFileService
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly PackageWideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly FileDownloader _fileDownloader;
        private readonly MZipFormat _mzipFormat;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILogger<PackageFileService> _logger;

        public PackageFileService(
            PackageWideEntityService wideEntityService,
            FlatContainerClient flatContainerClient,
            FileDownloader fileDownloader,
            MZipFormat mzipFormat,
            IOptions<NuGetInsightsSettings> options,
            ILogger<PackageFileService> logger)
        {
            _initializationState = ContainerInitializationState.New(InitializeInternalAsync, DestroyInternalAsync);
            _wideEntityService = wideEntityService;
            _flatContainerClient = flatContainerClient;
            _fileDownloader = fileDownloader;
            _mzipFormat = mzipFormat;
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
            await _wideEntityService.InitializeAsync(_options.Value.PackageArchiveTableName);
        }

        private async Task DestroyInternalAsync()
        {
            await _wideEntityService.DeleteTableAsync(_options.Value.PackageArchiveTableName);
        }

        public async Task<PrimarySignature?> GetPrimarySignatureAsync(IPackageIdentityCommit leafItem)
        {
            var info = await GetOrUpdateInfoFromLeafItemAsync(leafItem);
            if (!info.Available)
            {
                return null;
            }

            using var srcStream = info.SignatureBytes.AsStream();
            return PrimarySignature.Load(srcStream);
        }

        public async Task<ZipDirectory?> GetZipDirectoryAsync(IPackageIdentityCommit leafItem)
        {
            var info = await GetZipDirectoryAndLengthAsync(leafItem);
            return info?.Directory;
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

        public async Task<IReadOnlyDictionary<IPackageIdentityCommit, PackageFileInfoV1>> UpdateBatchAsync(string id, IReadOnlyCollection<IPackageIdentityCommit> leafItems)
        {
            return await _wideEntityService.UpdateBatchAsync(
                _options.Value.PackageArchiveTableName,
                id,
                leafItems,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        public async Task<PackageFileInfoV1> GetOrUpdateInfoFromLeafItemAsync(IPackageIdentityCommit leafItem)
        {
            return await _wideEntityService.GetOrUpdateInfoAsync(
                _options.Value.PackageArchiveTableName,
                leafItem,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        private async Task<PackageFileInfoV1> GetInfoAsync(IPackageIdentityCommit leafItem)
        {
            if (leafItem.LeafType == CatalogLeafType.PackageDelete)
            {
                return MakeDeletedInfo(leafItem);
            }

            var url = await _flatContainerClient.GetPackageContentUrlAsync(leafItem.PackageId, leafItem.PackageVersion);

            using var reader = await _fileDownloader.GetZipDirectoryReaderAsync(
                leafItem.PackageId,
                leafItem.PackageVersion,
                ArtifactFileType.Nupkg,
                url);

            if (reader is null)
            {
                return MakeDeletedInfo(leafItem);
            }

            return await GetInfoAsync(leafItem, reader.Properties, reader, url);
        }

        private async Task<PackageFileInfoV1> GetInfoAsync(
            IPackageIdentityCommit leafItem,
            ILookup<string, string> headers,
            ZipDirectoryReader reader,
            string url)
        {
            var zipDirectory = await reader.ReadAsync();
            var signatureBytes = await _fileDownloader.GetSignatureBytesAsync(reader, zipDirectory, leafItem.PackageId, leafItem.PackageVersion, url);

            var signedCms = new SignedCms();
            signedCms.Decode(signatureBytes);

            using var destStream = new MemoryStream();
            await _mzipFormat.WriteAsync(reader.Stream, destStream);

            return new PackageFileInfoV1
            {
                CommitTimestamp = leafItem.CommitTimestamp,
                Available = true,
                HttpHeaders = headers,
                MZipBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
                SignatureBytes = signatureBytes.AsMemory(),
            };
        }

        private static PackageFileInfoV1 MakeDeletedInfo(IPackageIdentityCommit leafItem)
        {
            return new PackageFileInfoV1
            {
                CommitTimestamp = leafItem.CommitTimestamp,
                Available = false,
            };
        }

        private static PackageFileInfoV1 DataToOutput(PackageFileInfoVersions data)
        {
            return data.V1;
        }

        private static PackageFileInfoVersions OutputToData(PackageFileInfoV1 output)
        {
            return new PackageFileInfoVersions(output);
        }

        [MessagePackObject]
        public class PackageFileInfoVersions : PackageWideEntityService.IPackageWideEntity
        {
            [SerializationConstructor]
            public PackageFileInfoVersions(PackageFileInfoV1 v1)
            {
                V1 = v1;
            }

            [Key(0)]
            public PackageFileInfoV1 V1 { get; set; }

            DateTimeOffset? PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
        }

        [MessagePackObject]
        public class PackageFileInfoV1
        {
            [Key(1)]
            public DateTimeOffset? CommitTimestamp { get; set; }

            [Key(2)]
            public bool Available { get; set; }

            [Key(3)]
            public ILookup<string, string>? HttpHeaders { get; set; }

            [Key(4)]
            public Memory<byte> MZipBytes { get; set; }

            [Key(5)]
            public Memory<byte> SignatureBytes { get; set; }
        }
    }
}
