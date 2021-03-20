using System.Collections.Generic;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.WideEntities;
using MessagePack;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public class VersionSetAggregateStorageService
    {
        private readonly WideEntityService _wideEntityService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public VersionSetAggregateStorageService(
            WideEntityService wideEntityService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _wideEntityService = wideEntityService;
            _options = options;
        }

        public async Task InitializeAsync(string storageSuffix)
        {
            await _wideEntityService.CreateTableAsync(GetTableName(storageSuffix));
        }

        public async Task DeleteTableAsync(string storageSuffix)
        {
            await _wideEntityService.DeleteTableAsync(GetTableName(storageSuffix));
        }

        public async Task<IEnumerable<CatalogPageData>> GetDescendingPagesAsync(string storageSuffix)
        {
            var entities = await _wideEntityService.RetrieveAsync(GetTableName(storageSuffix));
            return DeserializePages(entities);
        }

        private static IEnumerable<CatalogPageData> DeserializePages(IReadOnlyList<WideEntity> entities)
        {
            foreach (var entity in entities)
            {
                using var stream = entity.GetStream();
                yield return MessagePackSerializer.Deserialize<CatalogPageData>(
                    stream,
                    ExplorePackagesMessagePack.Options);
            }
        }

        public async Task AddPageAsync(string storageSuffix, CatalogPageData page)
        {
            var bytes = MessagePackSerializer.Serialize(
                page,
                ExplorePackagesMessagePack.Options);

            // Insert pages in reverse chronological order.
            await _wideEntityService.InsertAsync(
                GetTableName(storageSuffix),
                partitionKey: StorageUtility.GetDescendingId(page.CommitTimestamp),
                rowKey: StorageUtility.GenerateDescendingId().ToString(),
                bytes);
        }

        private string GetTableName(string storageSuffix)
        {
            return $"{_options.Value.VersionSetAggregateTableName}{storageSuffix}";
        }
    }
}
