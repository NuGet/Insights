// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Knapcode.MiniZip;
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
        private readonly SymbolPackageHashService _symbolPackageHashService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public SymbolPackageArchiveToCsvDriver(
            CatalogClient catalogClient,
            SymbolPackageFileService symbolPackageFileService,
            SymbolPackageHashService symbolPackageHashService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _symbolPackageFileService = symbolPackageFileService;
            _symbolPackageHashService = symbolPackageHashService;
            _options = options;
        }

        string ICsvResultStorage<SymbolPackageArchiveRecord>.ResultContainerName => _options.Value.SymbolPackageArchiveContainerName;
        string ICsvResultStorage<SymbolPackageArchiveEntry>.ResultContainerName => _options.Value.SymbolPackageArchiveEntryContainerName;
        public bool SingleMessagePerId => false;

        public async Task InitializeAsync()
        {
            await _symbolPackageFileService.InitializeAsync();
            await _symbolPackageHashService.InitializeAsync();
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<CsvRecordSets<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            (var archive, var entries) = await ProcessLeafInternalAsync(leafScan);
            return DriverResult.Success(new CsvRecordSets<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>(
                archive != null ? [archive] : [],
                entries ?? []));
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
                    [new SymbolPackageArchiveEntry(scanId, scanTimestamp, leaf)]
                );
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var hashes = await _symbolPackageHashService.GetHashesAsync(leafScan.ToPackageIdentityCommit(), requireFresh: false);
                if (hashes is null)
                {
                    return MakeDoesNotExist(scanId, scanTimestamp, leaf);
                }

                (var zipDirectory, var size, var headers) = await _symbolPackageFileService.GetZipDirectoryFromLeafItemAsync(leafScan.ToPackageIdentityCommit());
                if (zipDirectory is null)
                {
                    return MakeDoesNotExist(scanId, scanTimestamp, leaf);
                }

                // Necessary because of https://github.com/neuecc/MessagePack-CSharp/issues/1431
                var headerMD5 = !_options.Value.SkipContentMD5HeaderInCsv && headers.Contains(MD5Header) ? headers[MD5Header].SingleOrDefault() : null;
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

                    MD5 = hashes.MD5.ToBase64(),
                    SHA1 = hashes.SHA1.ToBase64(),
                    SHA256 = hashes.SHA256.ToBase64(),
                    SHA512 = hashes.SHA512.ToBase64(),
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

        private static (SymbolPackageArchiveRecord, IReadOnlyList<SymbolPackageArchiveEntry>) MakeDoesNotExist(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return (
                new SymbolPackageArchiveRecord(scanId, scanTimestamp, leaf) { ResultType = ArchiveResultType.DoesNotExist },
                [new SymbolPackageArchiveEntry(scanId, scanTimestamp, leaf) { ResultType = ArchiveResultType.DoesNotExist }]
            );
        }
    }
}