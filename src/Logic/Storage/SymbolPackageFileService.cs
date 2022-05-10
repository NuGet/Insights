// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using MessagePack;
using Microsoft.Extensions.Options;
using Microsoft.Toolkit.HighPerformance;

#nullable enable

namespace NuGet.Insights
{
    public class SymbolPackageFileService
    {
        private readonly PackageWideEntityService _wideEntityService;
        private readonly FileDownloader _fileDownloader;
        private readonly MZipFormat _mzipFormat;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public SymbolPackageFileService(
            PackageWideEntityService wideEntityService,
            FileDownloader fileDownloader,
            MZipFormat mzipFormat,
            IOptions<NuGetInsightsSettings> options)
        {
            _wideEntityService = wideEntityService;
            _fileDownloader = fileDownloader;
            _mzipFormat = mzipFormat;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await _wideEntityService.InitializeAsync(_options.Value.SymbolPackageArchiveTableName);
        }

        public async Task<(ZipDirectory? directory, long size, ILookup<string, string>? headers)> GetZipDirectoryAsync(ICatalogLeafItem leafItem)
        {
            var info = await GetOrUpdateInfoAsync(leafItem);
            if (!info.Available)
            {
                return (null, 0, null);
            }

            using var srcStream = info.MZipBytes.AsStream();
            using var destStream = await _mzipFormat.ReadAsync(srcStream);
            var reader = new ZipDirectoryReader(destStream);
            return (await reader.ReadAsync(), destStream.Length, info.HttpHeaders);
        }

        public async Task<IReadOnlyDictionary<ICatalogLeafItem, PackageFileInfoV1>> UpdateBatchAsync(string id, IReadOnlyCollection<ICatalogLeafItem> leafItems)
        {
            return await _wideEntityService.UpdateBatchAsync(
                _options.Value.SymbolPackageArchiveTableName,
                id,
                leafItems,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        public async Task<PackageFileInfoV1> GetOrUpdateInfoAsync(ICatalogLeafItem leafItem)
        {
            return await _wideEntityService.GetOrUpdateInfoAsync(
                _options.Value.SymbolPackageArchiveTableName,
                leafItem,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        private async Task<PackageFileInfoV1> GetInfoAsync(ICatalogLeafItem leafItem)
        {
            if (leafItem.Type == CatalogLeafType.PackageDelete)
            {
                return MakeDeletedInfo(leafItem);
            }

            var url = $"{_options.Value.SymbolPackagesContainerBaseUrl.TrimEnd('/')}/" +
                $"{leafItem.PackageId.ToLowerInvariant()}." +
                $"{leafItem.ParsePackageVersion().ToNormalizedString().ToLowerInvariant()}.snupkg";

            using var reader = await _fileDownloader.GetZipDirectoryReaderAsync(
                leafItem.PackageId,
                leafItem.PackageVersion,
                ArtifactFileType.Snupkg,
                url);

            if (reader is null)
            {
                return MakeDeletedInfo(leafItem);
            }

            return await GetInfoAsync(leafItem, reader.Properties, reader);
        }

        private async Task<PackageFileInfoV1> GetInfoAsync(
            ICatalogLeafItem leafItem,
            ILookup<string, string> headers,
            ZipDirectoryReader reader)
        {
            using var destStream = new MemoryStream();
            await _mzipFormat.WriteAsync(reader.Stream, destStream);
            return new PackageFileInfoV1
            {
                CommitTimestamp = leafItem.CommitTimestamp,
                Available = true,
                HttpHeaders = headers,
                MZipBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
            };
        }

        private static PackageFileInfoV1 MakeDeletedInfo(ICatalogLeafItem leafItem)
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

            DateTimeOffset PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
        }

        [MessagePackObject]
        public class PackageFileInfoV1
        {
            [Key(1)]
            public DateTimeOffset CommitTimestamp { get; set; }

            [Key(2)]
            public bool Available { get; set; }

            [Key(3)]
            public ILookup<string, string>? HttpHeaders { get; set; }

            [Key(4)]
            public Memory<byte> MZipBytes { get; set; }
        }
    }
}
