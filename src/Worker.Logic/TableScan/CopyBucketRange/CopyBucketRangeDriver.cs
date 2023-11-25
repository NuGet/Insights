// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.CopyBucketRange
{
    public class CopyBucketRangeDriver : ITableScanDriver<BucketedPackage>
    {
        private readonly BucketedPackageService _bucketedPackageService;
        private readonly SchemaSerializer _serializer;
        private readonly CatalogScanStorageService _storageService;

        public CopyBucketRangeDriver(
            BucketedPackageService bucketedPackageService,
            SchemaSerializer schemaSerializer,
            CatalogScanStorageService storageService)
        {
            _bucketedPackageService = bucketedPackageService;
            _serializer = schemaSerializer;
            _storageService = storageService;
        }

        public IList<string> SelectColumns => null;

        public async Task InitializeAsync(JsonElement? parameters)
        {
            await _bucketedPackageService.InitializeAsync();
        }

        public async Task ProcessEntitySegmentAsync(string tableName, JsonElement? parameters, IReadOnlyList<BucketedPackage> entities)
        {
            var deserializedParameters = (CopyBucketRangeParameters)_serializer.Deserialize(parameters.Value).Data;

            var indexScan = await _storageService.GetIndexScanAsync(deserializedParameters.DriverType, deserializedParameters.ScanId);

            var leafScans = entities
                .Select(bucketedPackage => new CatalogLeafScan(
                    storageSuffix: indexScan.StorageSuffix,
                    scanId: deserializedParameters.ScanId,
                    pageId: $"{bucketedPackage.PartitionKey}-{bucketedPackage.PackageId.ToLowerInvariant()}",
                    leafId: bucketedPackage.ParsePackageVersion().ToNormalizedString().ToLowerInvariant())
                {
                    DriverType = indexScan.DriverType,
                    Url = bucketedPackage.Url,
                    PageUrl = bucketedPackage.PageUrl,
                    LeafType = bucketedPackage.LeafType,
                    CommitId = bucketedPackage.CommitId,
                    CommitTimestamp = bucketedPackage.CommitTimestamp,
                    PackageId = bucketedPackage.PackageId,
                    PackageVersion = bucketedPackage.PackageVersion,
                    Min = indexScan.Min,
                    Max = indexScan.Max,
                    BucketRanges = indexScan.BucketRanges,
                })
                .ToList();

            await _storageService.InsertMissingAsync(leafScans, allowExtra: true);
        }
    }
}
