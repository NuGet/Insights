using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.FindLatestPackageLeaf
{
    public class LatestPackageLeafStorageFactory : ILatestPackageLeafStorageFactory<LatestPackageLeaf>
    {
        private readonly SchemaSerializer _serializer;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly CatalogScanService _catalogScanService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public LatestPackageLeafStorageFactory(
            SchemaSerializer serializer,
            ServiceClientFactory serviceClientFactory,
            CatalogScanService catalogScanService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serializer = serializer;
            _serviceClientFactory = serviceClientFactory;
            _catalogScanService = catalogScanService;
            _options = options;
        }

        public string GetTableName(string storageSuffix)
        {
            return $"{_options.Value.LatestPackageLeafTableName}{storageSuffix}";
        }

        public Task DeleteTableAsync(string storageSuffix)
        {
            return GetTable(storageSuffix).DeleteIfExistsAsync();
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            var parameters = DeserializeParameters(indexScan.DriverParameters);
            if (parameters.StorageSuffix == string.Empty)
            {
                if (indexScan.CursorName != _catalogScanService.GetCursorName(CatalogScanDriverType.FindLatestPackageLeaf))
                {
                    throw new NotSupportedException("When using the primary latest leaf table, only the main cursor name is allowed.");
                }
            }
            else
            {
                if (indexScan.CursorName != string.Empty)
                {
                    throw new NotSupportedException("When using the non-primary main latest leaf table, no cursor is allowed.");
                }
            }

            var table = GetTable(parameters.StorageSuffix);
            await table.CreateIfNotExistsAsync(retry: true);
        }

        public Task<ILatestPackageLeafStorage<LatestPackageLeaf>> CreateAsync(CatalogPageScan pageScan, IReadOnlyDictionary<CatalogLeafItem, int> leafItemToRank)
        {
            var parameters = DeserializeParameters(pageScan.DriverParameters);
            var table = GetTable(parameters.StorageSuffix);
            var storage = new LatestPackageLeafStorage(
                table,
                parameters.Prefix,
                leafItemToRank,
                pageScan.Rank,
                pageScan.Url);
            return Task.FromResult<ILatestPackageLeafStorage<LatestPackageLeaf>>(storage);
        }

        private LatestPackageLeafParameters DeserializeParameters(string parameters)
        {
            var deserializedParameters = (LatestPackageLeafParameters)_serializer.Deserialize(parameters).Data;
            if (deserializedParameters.StorageSuffix == string.Empty)
            {
                if (deserializedParameters.Prefix != string.Empty)
                {
                    throw new NotSupportedException("When using the primary latest leaves table, only an empty prefix is allowed.");
                }
            }

            return deserializedParameters;
        }

        private CloudTable GetTable(string storageSuffix)
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(GetTableName(storageSuffix));
        }
    }
}
