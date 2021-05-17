using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Insights.Worker.LoadPackageVersion;
using NuGet.Versioning;

namespace NuGet.Insights.Worker.PackageVersionToCsv
{
    public class PackageVersionToCsvDriver : ICatalogLeafToCsvDriver<PackageVersionRecord>, ICsvResultStorage<PackageVersionRecord>
    {
        private readonly PackageVersionStorageService _storageService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageVersionToCsvDriver(
            PackageVersionStorageService storageService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _storageService = storageService;
            _options = options;
        }

        public string ResultContainerName => _options.Value.PackageVersionContainerName;
        public bool SingleMessagePerId => true;

        public List<PackageVersionRecord> Prune(List<PackageVersionRecord> records)
        {
            return PackageRecord.Prune(records);
        }

        public async Task InitializeAsync()
        {
            await _storageService.InitializeAsync();
        }

        public async Task<DriverResult<CsvRecordSet<PackageVersionRecord>>> ProcessLeafAsync(CatalogLeafItem item, int attemptCount)
        {
            var records = await ProcessLeafInternalAsync(item);
            return DriverResult.Success(new CsvRecordSet<PackageVersionRecord>(bucketKey: item.PackageId.ToLowerInvariant(), records: records));
        }

        private async Task<List<PackageVersionRecord>> ProcessLeafInternalAsync(CatalogLeafItem item)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            // Fetch all of the known versions for this package ID.
            var entities = await _storageService.GetAsync(item.PackageId);

            // Find the set of versions that can possibly the latest.
            var versions = entities
                .Where(x => x.LeafType != CatalogLeafType.PackageDelete) // Deleted versions can't be the latest
                .Where(x => x.IsListed.Value) // Only listed versions can be latest
                .Select(x => (Entity: x, Version: NuGetVersion.Parse(x.PackageVersion), IsSemVer2: x.SemVerType.Value.IsSemVer2()))
                .OrderByDescending(x => x.Version)
                .ToList();
            var semVer1Versions = versions.Where(x => !x.IsSemVer2).ToList();

            // Determine the four definitions of "latest". Reminds me of NuGet.org Azure Search implementation...
            var latest = semVer1Versions.FirstOrDefault();
            var latestStable = semVer1Versions.Where(x => !x.Version.IsPrerelease).FirstOrDefault();
            var latestSemVer2 = versions.FirstOrDefault();
            var latestStableSemVer2 = versions.Where(x => !x.Version.IsPrerelease).FirstOrDefault();

            // Map all entities to CSV records.
            var records = new List<PackageVersionRecord>();
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                records.Add(new PackageVersionRecord(scanId, scanTimestamp, entity)
                {
                    SemVerOrder = entities.Count - (i + 1),
                    IsLatest = ReferenceEquals(entity, latest.Entity),
                    IsLatestStable = ReferenceEquals(entity, latestStable.Entity),
                    IsLatestSemVer2 = ReferenceEquals(entity, latestSemVer2.Entity),
                    IsLatestStableSemVer2 = ReferenceEquals(entity, latestStableSemVer2.Entity),
                });
            }

            return records;
        }

        public Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(PackageVersionRecord record)
        {
            throw new NotImplementedException();
        }
    }
}
