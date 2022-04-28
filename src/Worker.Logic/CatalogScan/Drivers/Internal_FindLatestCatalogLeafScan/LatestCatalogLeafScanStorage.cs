// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Azure.Data.Tables;
using NuGet.Versioning;

namespace NuGet.Insights.Worker.FindLatestCatalogLeafScan
{
    public class LatestCatalogLeafScanStorage : ILatestPackageLeafStorage<CatalogLeafScan>
    {
        private readonly CatalogIndexScan _indexScan;
        private readonly string _pageUrl;

        public LatestCatalogLeafScanStorage(TableClient table, CatalogIndexScan indexScan, string pageUrl)
        {
            Table = table;
            _indexScan = indexScan;
            _pageUrl = pageUrl;
        }

        public TableClient Table { get; }
        public string CommitTimestampColumnName => nameof(CatalogLeafScan.CommitTimestamp);

        public string GetPartitionKey(string packageId)
        {
            return CatalogLeafScan.GetPartitionKey(_indexScan.GetScanId(), GetPageId(packageId));
        }

        public string GetRowKey(string packageVersion)
        {
            return GetLeafId(packageVersion);
        }

        public Task<CatalogLeafScan> MapAsync(ICatalogLeafItem item)
        {
            return Task.FromResult(new CatalogLeafScan(_indexScan.StorageSuffix, _indexScan.GetScanId(), GetPageId(item.PackageId), GetLeafId(item.PackageVersion))
            {
                DriverType = _indexScan.DriverType,
                DriverParameters = _indexScan.DriverParameters,
                Url = item.Url,
                PageUrl = _pageUrl,
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

        private static string GetLeafId(string packageVersion)
        {
            return NuGetVersion.Parse(packageVersion).ToNormalizedString().ToLowerInvariant();
        }
    }
}
