// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker
{
    public class CatalogRangeSameAsBucketRange : EndToEndTest
    {
        public CatalogRangeSameAsBucketRange(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task Execute()
        {
            // Arrange
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultStorageBucketCount = 1;
                x.RecordCertificateStatus = false;
                x.DisabledDrivers = CatalogScanDriverMetadata.StartableDriverTypes
                    .Where(x => !CatalogScanDriverMetadata.GetBucketRangeSupport(x))
                    .Except(new[] { CatalogScanDriverType.LoadBucketedPackage })
                    .ToList();
            };

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);

            var expectedContainers = new List<(string ContainerName, Type RecordType, string DefaultTableName)>();
            foreach (var recordType in CsvRecordContainers.RecordTypes)
            {
                var producer = CsvRecordContainers.GetProducer(recordType);
                if (producer.Type == CsvRecordProducerType.CatalogScanDriver
                    && !Options.Value.DisabledDrivers.Contains(producer.CatalogScanDriverType.Value))
                {
                    expectedContainers.Add((
                        CsvRecordContainers.GetContainerName(recordType),
                        recordType,
                        CsvRecordContainers.GetDefaultKustoTableName(recordType)));
                }
            }

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, min0);
            await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, max1);
            await SetCursorsAsync(CatalogScanDriverMetadata.StartableDriverTypes, max1);
            var buckets = Enumerable.Range(0, BucketedPackage.BucketCount).ToList();

            // Act
            Output.WriteHorizontalRule();
            Output.WriteLine("Beginning bucket range processing.");
            Output.WriteHorizontalRule();

            foreach (var batch in CatalogScanDriverMetadata.GetParallelBatches(
                CatalogScanDriverMetadata.StartableDriverTypes.Where(CatalogScanDriverMetadata.GetBucketRangeSupport).ToHashSet(),
                Options.Value.DisabledDrivers.ToHashSet()))
            {
                var scans = new List<CatalogIndexScan>();
                foreach (var driverType in batch)
                {
                    var descendingId = StorageUtility.GenerateDescendingId();
                    var scanId = CatalogScanService.GetBucketRangeScanId(buckets, descendingId);
                    var scan = await CatalogScanService.UpdateAsync(
                        scanId,
                        descendingId.Unique,
                        driverType,
                        buckets);
                    Assert.Equal(CatalogScanServiceResultType.NewStarted, scan.Type);
                    scans.Add(scan.Scan);
                }

                foreach (var scan in scans)
                {
                    await UpdateAsync(scan, parallel: true);
                }
            }

            // Assert
            foreach (var (containerName, recordType, defaultTableName) in expectedContainers)
            {
                await AssertCsvAsync(recordType, containerName, nameof(CatalogRangeSameAsBucketRange), Step1, 0, $"{defaultTableName}.csv");
                await (await GetBlobAsync(containerName, $"compact_0.csv.gz")).DeleteAsync();
            }

            // Arrange
            await SetCursorsAsync(CatalogScanDriverMetadata.StartableDriverTypes, min0);

            // Act
            Output.WriteHorizontalRule();
            Output.WriteLine("Beginning catalog range processing.");
            Output.WriteHorizontalRule();

            foreach (var batch in CatalogScanDriverMetadata.GetParallelBatches(
                CatalogScanDriverMetadata.StartableDriverTypes.Where(CatalogScanDriverMetadata.GetBucketRangeSupport).ToHashSet(),
                Options.Value.DisabledDrivers.ToHashSet()))
            {
                var scans = new List<CatalogIndexScan>();
                foreach (var driverType in batch)
                {
                    var scan = await CatalogScanService.UpdateAsync(driverType, max1);
                    Assert.Equal(CatalogScanServiceResultType.NewStarted, scan.Type);
                    scans.Add(scan.Scan);
                }

                foreach (var scan in scans)
                {
                    await UpdateAsync(scan, parallel: true);
                }
            }

            // Assert
            foreach (var (containerName, recordType, defaultTableName) in expectedContainers)
            {
                await AssertCsvAsync(recordType, containerName, nameof(CatalogRangeSameAsBucketRange), Step1, 0, $"{defaultTableName}.csv");
            }
        }
    }
}
