// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Options;
using static NuGet.Insights.StorageUtility;

namespace NuGet.Insights.Worker.SymbolPackageArchiveToCsv
{
    public class SymbolPackageArchiveToCsvDriver :
        ICatalogLeafToCsvDriver<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>,
        ICsvResultStorage<SymbolPackageArchiveRecord>,
        ICsvResultStorage<SymbolPackageArchiveEntry>
    {
        private readonly CatalogClient _catalogClient;
        private readonly SymbolPackageFileService _symbolPackageFileService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public SymbolPackageArchiveToCsvDriver(
            CatalogClient catalogClient,
            SymbolPackageFileService symbolPackageFileService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _symbolPackageFileService = symbolPackageFileService;
            _options = options;
        }

        string ICsvResultStorage<SymbolPackageArchiveRecord>.ResultContainerName => _options.Value.SymbolPackageArchiveContainerName;
        string ICsvResultStorage<SymbolPackageArchiveEntry>.ResultContainerName => _options.Value.SymbolPackageArchiveEntryContainerName;
        public bool SingleMessagePerId => false;

        public List<SymbolPackageArchiveRecord> Prune(List<SymbolPackageArchiveRecord> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public List<SymbolPackageArchiveEntry> Prune(List<SymbolPackageArchiveEntry> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public Task<(ICatalogLeafItem LeafItem, string PageUrl)> MakeReprocessItemOrNullAsync(SymbolPackageArchiveRecord record)
        {
            throw new NotImplementedException();
        }

        public Task<(ICatalogLeafItem LeafItem, string PageUrl)> MakeReprocessItemOrNullAsync(SymbolPackageArchiveEntry record)
        {
            throw new NotImplementedException();
        }

        public async Task InitializeAsync()
        {
            await _symbolPackageFileService.InitializeAsync();
        }

        public async Task<DriverResult<CsvRecordSets<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            (var archive, var entries) = await ProcessLeafInternalAsync(leafScan);
            var bucketKey = PackageRecord.GetBucketKey(leafScan);
            return DriverResult.Success(new CsvRecordSets<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>(
                new CsvRecordSet<SymbolPackageArchiveRecord>(bucketKey, archive != null ? new[] { archive } : Array.Empty<SymbolPackageArchiveRecord>()),
                new CsvRecordSet<SymbolPackageArchiveEntry>(bucketKey, entries ?? Array.Empty<SymbolPackageArchiveEntry>())));
        }

        private async Task<(SymbolPackageArchiveRecord, IReadOnlyList<SymbolPackageArchiveEntry>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);
                return (
                    new SymbolPackageArchiveRecord(scanId, scanTimestamp, leaf),
                    new[] { new SymbolPackageArchiveEntry(scanId, scanTimestamp, leaf) }
                );
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                (var zipDirectory, var size, var headers) = await _symbolPackageFileService.GetZipDirectoryFromLeafItemAsync(leafScan);
                if (zipDirectory == null)
                {
                    return (
                        new SymbolPackageArchiveRecord(scanId, scanTimestamp, leaf) { ResultType = ArchiveResultType.DoesNotExist },
                        new[] { new SymbolPackageArchiveEntry(scanId, scanTimestamp, leaf) { ResultType = ArchiveResultType.DoesNotExist } }
                    );
                }

                // Necessary because of https://github.com/neuecc/MessagePack-CSharp/issues/1431
                var headerMD5 = headers.Contains(MD5Header) ? headers[MD5Header].SingleOrDefault() : null;
                var headerSHA512 = headers.Contains(SHA512Header) ? headers[SHA512Header].SingleOrDefault() : null;

                var archive = new SymbolPackageArchiveRecord(scanId, scanTimestamp, leaf)
                {
                    Size = size,
                    OffsetAfterEndOfCentralDirectory = zipDirectory.OffsetAfterEndOfCentralDirectory,
                    CentralDirectorySize = zipDirectory.CentralDirectorySize,
                    OffsetOfCentralDirectory = zipDirectory.OffsetOfCentralDirectory,
                    EntryCount = zipDirectory.Entries.Count,
                    Comment = zipDirectory.GetComment(),

                    HeaderMD5 = headerMD5,
                    HeaderSHA512 = headerSHA512,
                };
                var entries = new List<SymbolPackageArchiveEntry>();

                foreach (var entry in zipDirectory.Entries)
                {
                    var path = entry.GetName();

                    entries.Add(new SymbolPackageArchiveEntry(scanId, scanTimestamp, leaf)
                    {
                        SequenceNumber = entries.Count,

                        Path = path,
                        FileName = Path.GetFileName(path),
                        FileExtension = Path.GetExtension(path),
                        TopLevelFolder = PathUtility.GetTopLevelFolder(path),

                        Flags = entry.Flags,
                        CompressionMethod = entry.CompressionMethod,
                        LastModified = new DateTimeOffset(entry.GetLastModified(), TimeSpan.Zero),
                        Crc32 = entry.Crc32,
                        CompressedSize = entry.CompressedSize,
                        UncompressedSize = entry.UncompressedSize,
                        LocalHeaderOffset = entry.LocalHeaderOffset,
                        Comment = entry.GetComment(),
                    });
                }

                // NuGet symbol packages must contain contain at least a .nuspec file.
                if (!entries.Any())
                {
                    throw new InvalidOperationException($"ZIP archive has no entries for catalog leaf item {leafScan.Url}");
                }

                return (archive, entries);
            }
        }
    }
}
