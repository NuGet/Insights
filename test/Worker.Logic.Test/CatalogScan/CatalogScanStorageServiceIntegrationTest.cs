// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class CatalogScanStorageServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public class TheInsertAsyncMethodForIndexScan : CatalogScanStorageServiceIntegrationTest
        {
            [Fact]
            public async Task AllowsExistingIndexScanWithMatchingInfo()
            {
                await Target.InitializeAsync();
                var scan = new CatalogIndexScan(DriverType, StorageUtility.GenerateDescendingId().ToString(), StorageSuffix)
                {
                    State = CatalogIndexScanState.Created,
                };
                await Target.InsertAsync(scan);

                await Target.InsertAsync(scan);

                var actual = await Target.GetIndexScanAsync(DriverType, scan.ScanId);
                Assert.NotNull(actual);
            }

            public TheInsertAsyncMethodForIndexScan(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public class TheDeleteOlderIndexScansAsyncMethod : CatalogScanStorageServiceIntegrationTest
        {
            [Fact]
            public async Task DeletesOlderCompletedScans()
            {
                ConfigureWorkerSettings = x => x.OldCatalogIndexScansToKeep = 3;

                await Target.InitializeAsync();
                var scans = Enumerable
                    .Range(0, 10)
                    .Select(x => new CatalogIndexScan(DriverType, StorageUtility.GenerateDescendingId().ToString(), StorageSuffix)
                    {
                        State = CatalogIndexScanState.Complete,
                    })
                    .OrderBy(x => x.ScanId, StringComparer.Ordinal)
                    .ToList();
                await Task.WhenAll(scans.Select(x => Target.InsertAsync(x)));
                var currentScanId = scans.Skip(3).First().ScanId;

                await Target.DeleteOldIndexScansAsync(DriverType, currentScanId);

                var remainingScans = await (await ServiceClientFactory.GetTableServiceClientAsync())
                    .GetTableClient(Options.Value.CatalogIndexScanTableName)
                    .QueryAsync<CatalogIndexScan>()
                    .ToListAsync();
                Assert.Equal(scans.Take(7).Select(x => x.RowKey).ToList(), remainingScans.Select(x => x.RowKey).ToList());
            }

            [Fact]
            public async Task DoesNotDeleteIncompleteScans()
            {
                ConfigureWorkerSettings = x => x.OldCatalogIndexScansToKeep = 0;

                await Target.InitializeAsync();
                var scans = Enumerable
                    .Range(0, 3)
                    .Select(x => new CatalogIndexScan(DriverType, StorageUtility.GenerateDescendingId().ToString(), StorageSuffix)
                    {
                        State = CatalogIndexScanState.Created,
                    })
                    .OrderBy(x => x.ScanId, StringComparer.Ordinal)
                    .ToList();
                await Task.WhenAll(scans.Select(x => Target.InsertAsync(x)));
                var currentScanId = scans.First().ScanId;

                await Target.DeleteOldIndexScansAsync(DriverType, currentScanId);

                var remainingScans = await (await ServiceClientFactory.GetTableServiceClientAsync())
                    .GetTableClient(Options.Value.CatalogIndexScanTableName)
                    .QueryAsync<CatalogIndexScan>()
                    .ToListAsync();
                Assert.Equal(scans.Select(x => x.RowKey).ToList(), remainingScans.Select(x => x.RowKey).ToList());
            }

            public TheDeleteOlderIndexScansAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public CatalogScanStorageServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            DriverType = CatalogScanDriverType.PackageAssetToCsv;
            StorageSuffix = "bar";
        }

        public CatalogScanStorageService Target => CatalogScanStorageService;
        public CatalogScanDriverType DriverType { get; }
        public string StorageSuffix { get; }
    }
}
