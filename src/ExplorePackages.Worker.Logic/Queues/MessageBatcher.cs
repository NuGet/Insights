using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.FindLatestLeaves;
using Knapcode.ExplorePackages.Worker.TableCopy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class MessageBatcher : IMessageBatcher
    {
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<MessageBatcher> _logger;

        public MessageBatcher(
            CatalogScanStorageService catalogScanStorageService,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<MessageBatcher> logger)
        {
            _catalogScanStorageService = catalogScanStorageService;
            _options = options;
            _logger = logger;
        }

        public async Task<IReadOnlyList<HomogeneousBatchMessage>> BatchOrNullAsync<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> serializer)
        {
            var messageType = typeof(T);
            if (messageType == typeof(HomogeneousBatchMessage)
                || messages.Count <= 1
                || !_options.Value.AllowBatching)
            {
                return null;
            }

            var batchSize = 1;
            switch (messages.First())
            {
                case CatalogLeafScanMessage clsm:
                    var catalogLeafScan = await _catalogScanStorageService.GetLeafScanAsync(
                        clsm.StorageSuffix,
                        clsm.ScanId,
                        clsm.PageId,
                        clsm.LeafId);
                    if (catalogLeafScan.ParsedScanType == CatalogScanType.FindPackageAssets)
                    {
                        batchSize = 20;
                    }
                    break;
                case TableRowCopyMessage<LatestPackageLeaf>:
                    batchSize = 20;
                    break;
            }

            if (batchSize <= 1)
            {
                return null;
            }

            var batches = new List<HomogeneousBatchMessage>();
            foreach (var message in messages)
            {
                if (batches.Count == 0 || batches.Last().Messages.Count >= batchSize)
                {
                    batches.Add(new HomogeneousBatchMessage
                    {
                        SchemaName = serializer.Name,
                        SchemaVersion = serializer.LatestVersion,
                        Messages = new List<JToken>(),
                    });
                }

                batches.Last().Messages.Add(serializer.SerializeData(message).AsJToken());
            }

            _logger.LogInformation(
                "Batched {MessageCount} {SchemaName} messages into {BatchCount} batches of size {BatchSize}.",
                messages.Count,
                serializer.Name,
                batches.Count,
                batchSize);

            return batches;
        }
    }
}
