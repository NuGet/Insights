// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker.FindLatestCatalogLeafScanPerId
{
    public class LatestCatalogLeafScanPerIdStorage : ILatestPackageLeafStorage<CatalogLeafScanPerId>
    {
        private static readonly string LeafId = string.Empty;

        private readonly CatalogIndexScan _indexScan;

        public LatestCatalogLeafScanPerIdStorage(TableClient table, CatalogIndexScan indexScan)
        {
            Table = table;
            _indexScan = indexScan;
        }

        public TableClient Table { get; }
        public string CommitTimestampColumnName => nameof(CatalogLeafScan.CommitTimestamp);

        public string GetPartitionKey(string packageId)
        {
            return CatalogLeafScan.GetPartitionKey(_indexScan.GetScanId(), GetPageId(packageId));
        }

        public string GetRowKey(string packageVersion)
        {
            return LeafId;
        }

        public Task<CatalogLeafScanPerId> MapAsync(CatalogLeafItem item)
        {
            return Task.FromResult(new CatalogLeafScanPerId(_indexScan.StorageSuffix, _indexScan.GetScanId(), GetPageId(item.PackageId), LeafId)
            {
                DriverType = _indexScan.DriverType,
                DriverParameters = _indexScan.DriverParameters,
                Url = item.Url,
                LeafType = item.Type,
                CommitId = item.CommitId,
                CommitTimestamp = item.CommitTimestamp,
                PackageId = item.PackageId,
                PackageVersion = item.PackageVersion,
            });
        }

        private static string GetPageId(string packageId)
        {
            return packageId.ToLowerInvariant();
        }
    }
}
