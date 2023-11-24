// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class CatalogScanStorageServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public class TheReplaceAsyncMethodForIndexScan : CatalogScanStorageServiceIntegrationTest
        {
            [Fact]
            public async Task EmitsExpectedTelemetry()
            {
                await Target.InitializeAsync();
                var scan = new CatalogIndexScan
                {
                    BucketRanges = "23-42",
                    Completed = new DateTimeOffset(2023, 11, 18, 3, 0, 0, TimeSpan.Zero),
                    ContinueUpdate = true,
                    Created = new DateTimeOffset(2023, 11, 18, 1, 0, 0, TimeSpan.Zero),
                    CursorName = "my-cursor",
                    Min = new DateTimeOffset(2023, 1, 10, 10, 0, 0, TimeSpan.Zero),
                    Max = new DateTimeOffset(2023, 1, 10, 15, 0, 0, TimeSpan.Zero),
                    OnlyLatestLeaves = true,
                    ParentDriverType = CatalogScanDriverType.LoadPackageArchive,
                    ParentScanId = "my-parent-scan-id",
                    PartitionKey = CatalogScanDriverType.Internal_FindLatestCatalogLeafScan.ToString(),
                    Result = CatalogIndexScanResult.ExpandLatestLeaves,
                    RowKey = "my-scan-id",
                    Started = new DateTimeOffset(2023, 11, 18, 2, 0, 0, TimeSpan.Zero),
                    State = CatalogIndexScanState.Enqueuing,
                    StorageSuffix = "my-storage-suffix",
                };
                await Target.InsertAsync(scan);

                await Target.ReplaceAsync(scan);

                var formattedProperties = GetFormattedTelemetryProperties();
                Assert.Equal(
                    """
                    {
                      "BucketRanges": "23-42",
                      "Completed": "2023-11-18T03:00:00.0000000\u002B00:00",
                      "ContinueUpdate": "true",
                      "Created": "2023-11-18T01:00:00.0000000\u002B00:00",
                      "CursorName": "my-cursor",
                      "DriverType": "Internal_FindLatestCatalogLeafScan",
                      "Max": "2023-01-10T15:00:00.0000000\u002B00:00",
                      "Min": "2023-01-10T10:00:00.0000000\u002B00:00",
                      "OnlyLatestLeaves": "true",
                      "ParentDriverType": "LoadPackageArchive",
                      "ParentScanId": "my-parent-scan-id",
                      "Result": "ExpandLatestLeaves",
                      "ScanId": "my-scan-id",
                      "Started": "2023-11-18T02:00:00.0000000\u002B00:00",
                      "State": "Enqueuing",
                      "StorageSuffix": "my-storage-suffix"
                    }
                    """,
                    formattedProperties);
                var actualKeys = JsonSerializer
                    .Deserialize<Dictionary<string, string>>(formattedProperties)
                    .Keys
                    .Order(StringComparer.Ordinal);
                var expectedKeys = typeof(CatalogIndexScan)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Select(p => p.Name)
                    .Except(new[]
                    {
                        nameof(CatalogIndexScan.ClientRequestId),
                        nameof(CatalogIndexScan.ETag),
                        nameof(CatalogIndexScan.PartitionKey),
                        nameof(CatalogIndexScan.RowKey),
                        nameof(CatalogIndexScan.Timestamp),
                    })
                    .Order(StringComparer.Ordinal);
                Assert.Equal(expectedKeys, actualKeys);
            }

            private string GetFormattedTelemetryProperties()
            {
                var value = Assert.Single(TelemetryClient.MetricValues, x => x.MetricId == "CatalogIndexScan.RuntimeMinutes");
                Assert.Equal(60, value.MetricValue);

                return JsonSerializer.Serialize(
                    new SortedDictionary<string, string>(value.MetricProperties, StringComparer.Ordinal),
                    new JsonSerializerOptions { WriteIndented = true });
            }

            public TheReplaceAsyncMethodForIndexScan(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public class TheInsertAsyncMethodForIndexScan : CatalogScanStorageServiceIntegrationTest
        {
            [Fact]
            public async Task AllowsExistingIndexScanWithMatchingInfo()
            {
                await Target.InitializeAsync();
                var scan = new CatalogIndexScan(DriverType, StorageUtility.GenerateDescendingId().ToString(), StorageSuffix)
                {
                    State = CatalogIndexScanState.Created,
                }.SetDefaults();
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
                    }.SetDefaults())
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
                    }.SetDefaults())
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
