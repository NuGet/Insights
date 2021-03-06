using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.PackageArchiveEntryToCsv
{
    public class PackageArchiveEntryToCsvDriver : ICatalogLeafToCsvDriver<PackageArchiveEntry>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public PackageArchiveEntryToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _options = options;
        }

        public string ResultsContainerName => _options.Value.PackageArchiveEntryContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageArchiveEntry> Prune(List<PackageArchiveEntry> records)
        {
            return PackageRecord.Prune(records);
        }

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
        }

        public async Task<DriverResult<List<PackageArchiveEntry>>> ProcessLeafAsync(CatalogLeafItem item, int attemptCount)
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
                return DriverResult.Success(new List<PackageArchiveEntry> { new PackageArchiveEntry(scanId, scanTimestamp, leaf) });
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                var zipDirectory = await _packageFileService.GetZipDirectoryAsync(item);
                if (zipDirectory == null)
                {
                    // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted asset record.
                    return DriverResult.Success(new List<PackageArchiveEntry>());
                }

                var i = 0;
                var items = new List<PackageArchiveEntry>();

                foreach (var entry in zipDirectory.Entries)
                {
                    var path = entry.GetName();

                    items.Add(new PackageArchiveEntry(scanId, scanTimestamp, leaf, PackageArchiveEntryResultType.AvailableEntries)
                    {
                        SequenceNumber = i++,

                        Path = path,
                        FileName = Path.GetFileName(path),
                        FileExtension = Path.GetExtension(path),
                        TopLevelFolder = PathUtility.GetTopLevelFolder(path),

                        UncompressedSize = entry.UncompressedSize,
                        Crc32 = entry.Crc32,
                    });
                }

                // NuGet packages must contain contain at least a .nuspec file.
                if (!items.Any())
                {
                    throw new InvalidOperationException(
                        $"ZIP archive has no entries for catalog leaf item {item.Url}");
                }

                return DriverResult.Success(items);
            }
        }

        public string GetBucketKey(CatalogLeafItem item)
        {
            return PackageRecord.GetBucketKey(item);
        }

        public Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(PackageArchiveEntry record)
        {
            throw new NotImplementedException();
        }
    }
}