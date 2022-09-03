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

namespace NuGet.Insights.Worker.PackageArchiveToCsv
{
    public class PackageArchiveToCsvDriver :
        ICatalogLeafToCsvDriver<PackageArchiveRecord, PackageArchiveEntry>,
        ICsvResultStorage<PackageArchiveRecord>,
        ICsvResultStorage<PackageArchiveEntry>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly PackageHashService _packageHashService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageArchiveToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            PackageHashService packageHashService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _packageHashService = packageHashService;
            _options = options;
        }

        string ICsvResultStorage<PackageArchiveRecord>.ResultContainerName => _options.Value.PackageArchiveContainerName;
        string ICsvResultStorage<PackageArchiveEntry>.ResultContainerName => _options.Value.PackageArchiveEntryContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageArchiveRecord> Prune(List<PackageArchiveRecord> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public List<PackageArchiveEntry> Prune(List<PackageArchiveEntry> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public Task<(ICatalogLeafItem LeafItem, string PageUrl)> MakeReprocessItemOrNullAsync(PackageArchiveRecord record)
        {
            throw new NotImplementedException();
        }

        public Task<(ICatalogLeafItem LeafItem, string PageUrl)> MakeReprocessItemOrNullAsync(PackageArchiveEntry record)
        {
            throw new NotImplementedException();
        }

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
            await _packageHashService.InitializeAsync();
        }

        public async Task<DriverResult<CsvRecordSets<PackageArchiveRecord, PackageArchiveEntry>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            (var archive, var entries) = await ProcessLeafInternalAsync(leafScan);
            var bucketKey = PackageRecord.GetBucketKey(leafScan);
            return DriverResult.Success(new CsvRecordSets<PackageArchiveRecord, PackageArchiveEntry>(
                new CsvRecordSet<PackageArchiveRecord>(bucketKey, archive != null ? new[] { archive } : Array.Empty<PackageArchiveRecord>()),
                new CsvRecordSet<PackageArchiveEntry>(bucketKey, entries ?? Array.Empty<PackageArchiveEntry>())));
        }

        private async Task<(PackageArchiveRecord, IReadOnlyList<PackageArchiveEntry>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);
                return (
                    new PackageArchiveRecord(scanId, scanTimestamp, leaf),
                    new[] { new PackageArchiveEntry(scanId, scanTimestamp, leaf) }
                );
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                (var zipDirectory, var size, var headers) = await _packageFileService.GetZipDirectoryAndSizeAsync(leafScan);
                if (zipDirectory == null)
                {
                    // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted record.
                    return (null, null);
                }

                var hashes = await _packageHashService.GetHashesOrNullAsync(leafScan.PackageId, leafScan.PackageVersion);
                if (hashes == null)
                {
                    // Ignore packages where the hashes are missing. A subsequent scan will produce a deleted record.
                    return (null, null);
                }

                // Necessary because of https://github.com/neuecc/MessagePack-CSharp/issues/1431
                var headerMD5 = headers.Contains(MD5Header) ? headers[MD5Header].SingleOrDefault() : null;
                var headerSHA512 = headers.Contains(SHA512Header) ? headers[SHA512Header].SingleOrDefault() : null;

                var archive = new PackageArchiveRecord(scanId, scanTimestamp, leaf)
                {
                    Size = size,
                    OffsetAfterEndOfCentralDirectory = zipDirectory.OffsetAfterEndOfCentralDirectory,
                    CentralDirectorySize = zipDirectory.CentralDirectorySize,
                    OffsetOfCentralDirectory = zipDirectory.OffsetOfCentralDirectory,
                    EntryCount = zipDirectory.Entries.Count,
                    Comment = zipDirectory.GetComment(),

                    MD5 = hashes.MD5.ToBase64(),
                    SHA1 = hashes.SHA1.ToBase64(),
                    SHA256 = hashes.SHA256.ToBase64(),
                    SHA512 = hashes.SHA512.ToBase64(),
                    HeaderMD5 = headerMD5,
                    HeaderSHA512 = headerSHA512,
                };
                var entries = new List<PackageArchiveEntry>();

                foreach (var entry in zipDirectory.Entries)
                {
                    var path = entry.GetName();

                    entries.Add(new PackageArchiveEntry(scanId, scanTimestamp, leaf)
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

                // NuGet packages must contain contain at least a .nuspec file.
                if (!entries.Any())
                {
                    throw new InvalidOperationException($"ZIP archive has no entries for catalog leaf item {leafScan.Url}");
                }

                return (archive, entries);
            }
        }
    }
}
