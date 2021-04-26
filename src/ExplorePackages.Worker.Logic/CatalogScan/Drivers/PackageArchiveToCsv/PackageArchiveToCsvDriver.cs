using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.PackageArchiveToCsv
{
    public class PackageArchiveToCsvDriver :
        ICatalogLeafToCsvDriver<PackageArchiveRecord, PackageArchiveEntry>,
        ICsvResultStorage<PackageArchiveRecord>,
        ICsvResultStorage<PackageArchiveEntry>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly PackageHashService _packageHashService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public PackageArchiveToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            PackageHashService packageHashService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _packageHashService = packageHashService;
            _options = options;
        }

        string ICsvResultStorage<PackageArchiveRecord>.ResultContainerName => _options.Value.PackageArchiveContainerName;
        string ICsvResultStorage<PackageArchiveEntry>.ResultContainerName => _options.Value.PackageArchiveEntryContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageArchiveRecord> Prune(List<PackageArchiveRecord> records)
        {
            return PackageRecord.Prune(records);
        }

        public List<PackageArchiveEntry> Prune(List<PackageArchiveEntry> records)
        {
            return PackageRecord.Prune(records);
        }

        public Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(PackageArchiveRecord record)
        {
            throw new NotImplementedException();
        }

        public Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(PackageArchiveEntry record)
        {
            throw new NotImplementedException();
        }

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
            await _packageHashService.InitializeAsync();
        }

        public async Task<DriverResult<CsvRecordSets<PackageArchiveRecord, PackageArchiveEntry>>> ProcessLeafAsync(CatalogLeafItem item, int attemptCount)
        {
            (var archive, var entries) = await ProcessLeafInternalAsync(item);
            var bucketKey = PackageRecord.GetBucketKey(item);
            return DriverResult.Success(new CsvRecordSets<PackageArchiveRecord, PackageArchiveEntry>(
                new CsvRecordSet<PackageArchiveRecord>(bucketKey, archive != null ? new[] { archive } : Array.Empty<PackageArchiveRecord>()),
                new CsvRecordSet<PackageArchiveEntry>(bucketKey, entries ?? Array.Empty<PackageArchiveEntry>())));
        }

        private async Task<(PackageArchiveRecord, IReadOnlyList<PackageArchiveEntry>)> ProcessLeafInternalAsync(CatalogLeafItem item)
        {
            Guid? scanId = null;
            DateTimeOffset? scanTimestamp = null;
            if (_options.Value.AppendResultUniqueIds)
            {
                scanId = Guid.NewGuid();
                scanTimestamp = DateTimeOffset.UtcNow;
            }

            if (item.Type == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                return (
                    new PackageArchiveRecord(scanId, scanTimestamp, leaf),
                    new[] { new PackageArchiveEntry(scanId, scanTimestamp, leaf) }
                );
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                (var zipDirectory, var size) = await _packageFileService.GetZipDirectoryAndSizeAsync(item);
                if (zipDirectory == null)
                {
                    // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted asset record.
                    return (null, null);
                }

                var hashes = await _packageHashService.GetHashesOrNullAsync(item.PackageId, item.PackageVersion);
                if (hashes == null)
                {
                    // Ignore packages where the hashes are missing. A subsequent scan will produce a deleted asset record.
                    return (null, null);
                }

                var archive = new PackageArchiveRecord(scanId, scanTimestamp, leaf)
                {
                    Size = size,
                    MD5 = hashes.MD5.ToBase64(),
                    SHA1 = hashes.SHA1.ToBase64(),
                    SHA256 = hashes.SHA256.ToBase64(),
                    SHA512 = hashes.SHA512.ToBase64(),

                    OffsetAfterEndOfCentralDirectory = zipDirectory.OffsetAfterEndOfCentralDirectory,
                    CentralDirectorySize = zipDirectory.CentralDirectorySize,
                    OffsetOfCentralDirectory = zipDirectory.OffsetOfCentralDirectory,
                    EntryCount = zipDirectory.Entries.Count,
                    Comment = zipDirectory.GetComment(),
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
                    throw new InvalidOperationException($"ZIP archive has no entries for catalog leaf item {item.Url}");
                }

                return (archive, entries);
            }
        }
    }
}