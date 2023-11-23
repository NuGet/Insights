// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Extensions.Tables;
using Markdig.Helpers;
#if DEBUG
using Markdig.Renderers.Roundtrip;
#endif
using Markdig.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class DriverDocsTest : IClassFixture<DefaultWebApplicationFactory<StaticFilesStartup>>
    {
        /// <summary>
        /// Use this to overwrite existing driver docs. Do not enable this if you have uncommitted driver docs changes!
        /// </summary>
        public static readonly bool OverwriteDriverDocs = false;

        [DocsFact]
        public void AllDriversAreListedInREADME()
        {
            var info = new DocInfo(Path.Combine("drivers", "README.md"));
            info.ReadMarkdown();

            var table = info.GetTableAfterHeading("Drivers");

            // Verify the driver header
            var headerRow = Assert.IsType<TableRow>(table[0]);
            Assert.True(headerRow.IsHeader);
            Assert.Equal(2, headerRow.Count);
            Assert.Equal("Driver name", info.ToPlainText(headerRow[0]));
            Assert.Equal("Description", info.ToPlainText(headerRow[1]));

            // Verify we have the right number of rows
            var rows = table.Skip(1).ToList();

            // Verify table rows
            for (var i = 0; i < rows.Count && i < DriverNames.Count; i++)
            {
                Block rowObj = rows[i];
                _output.WriteLine("Testing row: " + info.GetMarkdown(rowObj));
                var row = Assert.IsType<TableRow>(rowObj);
                Assert.False(row.IsHeader);

                // Verify the column name exists and is in the proper order in the table
                var driverName = info.ToPlainText(row[0]);
                Assert.Contains(driverName, DriverNames);
                Assert.Equal(driverName, DriverNames[i]);

                // Verify the data type
                var description = info.ToPlainText(row[1]);
                Assert.NotEmpty(description);
            }

            Assert.Equal(DriverNames.Count, rows.Count);
        }

        [DocsTheory]
        [MemberData(nameof(DriverNameTestData))]
        public void DriverIsDocumented(string driverName)
        {
            var info = new DocInfo(Path.Combine("drivers", $"{driverName}.md"));
            Assert.True(File.Exists(info.DocPath), $"The {driverName} driver should be documented at {info.DocPath}");
        }

        [DocsFact]
        public void DriverDependencyGraphIsUpdated()
        {
            var info = new DocInfo(Path.Combine("drivers", "README.md"));
            info.ReadMarkdown();

            var codeBlock = info.GetFencedCodeBlockAfterHeading("Dependency graph");
            Assert.Equal("mermaid", codeBlock.Info);
            Assert.Equal('`', codeBlock.FencedChar);
            Assert.Equal(3, codeBlock.OpeningFencedCharCount);
            Assert.Equal(3, codeBlock.ClosingFencedCharCount);
            Assert.Equal(0, codeBlock.IndentCount);

            var expectedLines = new List<string>
            {
                "flowchart LR",
                $"    FlatContainer[<a href='https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource'>NuGet.org V3 package content</a>]"
            };
            foreach (var driverType in CatalogScanCursorService.StartableDriverTypes.OrderBy(x => x.ToString(), StringComparer.Ordinal))
            {
                // Relative links are not supported.
                // See: https://github.com/orgs/community/discussions/46096
                // See: https://github.com/mermaid-js/mermaid/issues/2233
                // expectedLines.Add($"    {driverType}[<a href='./{driverType}.md'>{driverType}</a>]");

                if (CatalogScanCursorService.GetFlatContainerDependents().Contains(driverType))
                {
                    expectedLines.Add($"    FlatContainer --> {driverType}");
                }

                foreach (var dependency in CatalogScanCursorService.GetDependencies(driverType).OrderBy(x => x.ToString(), StringComparer.Ordinal))
                {
                    expectedLines.Add($"    {dependency} --> {driverType}");
                }
            }

            var expected = string.Join(Environment.NewLine, expectedLines);
            var actual = string.Join(string.Empty, codeBlock.Lines);

#if DEBUG
            if (actual != expected)
            {
                var beforeCodeBlock = info.UnparsedMarkdown.Substring(0, codeBlock.Span.Start);
                var afterCodeBlock = info.UnparsedMarkdown.Substring(codeBlock.Span.End + 1);

                codeBlock.Lines.Clear();
                foreach (var line in expectedLines)
                {
                    codeBlock.Lines.Add(new StringSlice(line + Environment.NewLine));
                }

                using var writer = new StringWriter();
                var renderer = new RoundtripRenderer(writer);
                renderer.Write(codeBlock);
                File.WriteAllText(info.DocPath, beforeCodeBlock + writer.ToString().Trim() + afterCodeBlock);
            }
#else
            Assert.Equal(expected, actual);
#endif
        }

        [DocsTheory]
        [MemberData(nameof(DriverTypeTestData))]
        public async Task FirstTableIsGeneralDriverProperties(CatalogScanDriverType driverType)
        {
            var info = await GetDriverInfoAsync(driverType);
            info.ReadMarkdown();

            var table = info.MarkdownDocument.OfType<Table>().FirstOrDefault();
            Assert.NotNull(table);

            var rows = table.Cast<TableRow>().ToList();

            var i = 0;
            Assert.Equal(2, rows[i].Count);
            Assert.Empty(info.ToPlainText(rows[i]));

            i++;
            Assert.Equal("CatalogScanDriverType enum value", info.ToPlainText(rows[i][0]));
            Assert.NotEmpty(info.ToPlainText(rows[i][0]));
            var driverTypeValue = info.ToPlainText(rows[i][1]);
            Assert.Contains(driverTypeValue, driverType.ToString(), StringComparison.Ordinal);

            i++;
            Assert.Equal("Driver implementation", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Processing mode", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Cursor dependencies", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Components using driver output", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Temporary storage config", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Persistent storage config", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Output CSV tables", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal(i, rows.Count);
        }

        private static readonly ConcurrentDictionary<CatalogScanDriverType, DriverDocInfo> CachedDriverDocInfo = new();
        private static Lazy<Task> LazyDriverDocInfoTask;

        private async Task<DriverDocInfo> GetDriverInfoAsync(CatalogScanDriverType driverType)
        {
            Interlocked.CompareExchange(
                ref LazyDriverDocInfoTask,
                new Lazy<Task>(() => PopulateDriverInfoAsync(driverType)),
                null);

            while (!LazyDriverDocInfoTask.Value.IsCompleted && !CachedDriverDocInfo.ContainsKey(driverType))
            {
                await Task.Delay(100);
            }

            if (CachedDriverDocInfo.TryGetValue(driverType, out var info))
            {
                return info;
            }

            await LazyDriverDocInfoTask.Value;
            throw new InvalidOperationException($"No driver doc information could be found for {driverType}.");
        }

        private async Task PopulateDriverInfoAsync(CatalogScanDriverType firstDriverType)
        {
            TestExecution te = new TestExecution(_output, _factory);
            try
            {
                await PopulateDriverInfoAsync(te, firstDriverType);
            }
            finally
            {
                te.Output.WriteLine("Running test clean-up.");
                await te.DisposeAsync();
                te.Output.WriteLine("");
            }
        }

        private static async Task PopulateDriverInfoAsync(TestExecution te, CatalogScanDriverType firstDriverType)
        {
            var cursorService = te.Host.Services.GetRequiredService<CatalogScanCursorService>();
            var driverTypes = GetAllDriverTypesInOrder(cursorService, firstDriverType);
            var factory = te.Host.Services.GetRequiredService<ICatalogScanDriverFactory>();
            var enqueuer = te.Host.Services.GetRequiredService<IMessageEnqueuer>();

            te.Output.WriteLine("");
            te.Output.WriteLine("Initializing common storage.");
            await enqueuer.InitializeAsync();
            te.Output.WriteLine("");
            var containers = await GetContainersAsync(te);

            foreach (var driverType in driverTypes)
            {
                var driver = factory.Create(driverType);

                var (indexResult, pageResult, intermediateContainers, persistentContainers) = await RunDriverAsync(te, driver, driverType, containers);

                if (intermediateContainers.Any())
                {
                    te.Output.WriteLine("");
                    te.Output.WriteLine("Intermediate storage:");
                    foreach (var container in intermediateContainers)
                    {
                        te.Output.WriteLine($"  {container})");
                    }
                }

                if (persistentContainers.Any())
                {
                    te.Output.WriteLine("");
                    te.Output.WriteLine("Added storage:");
                    foreach (var container in persistentContainers)
                    {
                        te.Output.WriteLine($"  {container})");
                    }
                    containers.UnionWith(persistentContainers);
                }

                te.Output.WriteLine("");

                List<string> csvTables;
                if (driver is BaseCatalogLeafScanToCsvAdapter)
                {
                    csvTables = driver
                        .GetType()
                        .GetGenericArguments()
                        .Select(x => KustoDDL.TypeToDefaultTableName[x])
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToList();
                }
                else
                {
                    csvTables = new List<string>();
                }

                var info = new DriverDocInfo(
                    driverType,
                    indexResult,
                    pageResult,
                    intermediateContainers,
                    persistentContainers,
                    csvTables);

                CachedDriverDocInfo.TryAdd(driverType, info);
            }
        }

        private static void AddTransitiveDependencies(
            CatalogScanCursorService cursorService,
            HashSet<CatalogScanDriverType> uniqueDriverTypes,
            List<CatalogScanDriverType> driverTypesInOrder,
            CatalogScanDriverType driverType)
        {
            foreach (var dependency in CatalogScanCursorService.GetDependencies(driverType))
            {
                AddTransitiveDependencies(cursorService, uniqueDriverTypes, driverTypesInOrder, dependency);
            }

            if (uniqueDriverTypes.Add(driverType))
            {
                driverTypesInOrder.Add(driverType);
            }
        }

        private static List<CatalogScanDriverType> GetAllDriverTypesInOrder(CatalogScanCursorService cursorService, CatalogScanDriverType firstDriverType)
        {
            var uniqueDriverTypes = new HashSet<CatalogScanDriverType>();
            var driverTypesInOrder = new List<CatalogScanDriverType>();

            AddTransitiveDependencies(cursorService, uniqueDriverTypes, driverTypesInOrder, firstDriverType);
            foreach (var driverType in CatalogScanCursorService.StartableDriverTypes)
            {
                AddTransitiveDependencies(cursorService, uniqueDriverTypes, driverTypesInOrder, driverType);
            }

            return driverTypesInOrder;
        }

        private static async Task<HashSet<ConfiguredStorage>> GetContainersAsync(TestExecution testExecution)
        {
            var settings = testExecution
                .Host
                .Services
                .GetRequiredService<IOptions<NuGetInsightsWorkerSettings>>()
                .Value;

            var configurationValueToName = settings
                .GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.Public)
                .Where(x => x.PropertyType == typeof(string))
                .Select(x => new { ConfigurationValue = (string)x.GetValue(settings), ConfigurationName = x.Name })
                .Where(x => x.ConfigurationValue is not null && x.ConfigurationValue.StartsWith(testExecution.StoragePrefix, StringComparison.Ordinal))
                .ToDictionary(x => x.ConfigurationValue, x => x.ConfigurationName);

            var serviceClientFactory = testExecution.Host.Services.GetRequiredService<ServiceClientFactory>();

            var blobServiceClient = await serviceClientFactory.GetBlobServiceClientAsync();
            var blobContainerItems = await blobServiceClient.GetBlobContainersAsync().ToListAsync();
            var blobContainers = blobContainerItems
                .Select(x => (Type: StorageContainerType.BlobContainer, Name: configurationValueToName.GetValueOrDefault(x.Name), Value: x.Name));

            var queueServiceClient = await serviceClientFactory.GetQueueServiceClientAsync();
            var queueItems = await queueServiceClient.GetQueuesAsync().ToListAsync();
            var queues = queueItems
                .Select(x => (Type: StorageContainerType.Queue, Name: configurationValueToName.GetValueOrDefault(x.Name), Value: x.Name));

            var tableServiceClient = await serviceClientFactory.GetTableServiceClientAsync();
            var tableItems = await tableServiceClient.QueryAsync().ToListAsync();
            var tables = tableItems
                .Select(x => (Type: StorageContainerType.Table, Name: configurationValueToName.GetValueOrDefault(x.Name), Value: x.Name));

            var containerNameToStorage = blobContainers
                .Concat(queues)
                .Concat(tables)
                .Where(x => x.Value.StartsWith(testExecution.StoragePrefix, StringComparison.Ordinal))
                .ToDictionary(x => x.Value);

            var isStorageMatched = containerNameToStorage
                .GroupBy(x => x.Value.Name is not null, x => x.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var output = new HashSet<ConfiguredStorage>();

            if (isStorageMatched.ContainsKey(true))
            {
                output.UnionWith(isStorageMatched[true].Select(x => new ConfiguredStorage(x.Type, x.Name, Prefix: false)));
            }

            if (isStorageMatched.ContainsKey(false))
            {
                foreach (var storage in isStorageMatched[false])
                {
                    if (storage.Type == StorageContainerType.Queue
                        && storage.Value.EndsWith("-poison", StringComparison.Ordinal)
                        && configurationValueToName[storage.Value.Substring(0, storage.Value.Length - "-poison".Length)].Any())
                    {
                        continue;
                    }

                    // An intermediate storage container name should be prefixed by at most one configuration value.
                    var suffix = configurationValueToName
                        .Where(x => storage.Value.StartsWith(x.Key, StringComparison.Ordinal))
                        .SingleOrDefault();
                    if (suffix.Key is not null)
                    {
                        output.Add(new ConfiguredStorage(storage.Type, suffix.Value, Prefix: true));
                        continue;
                    }

                    throw new InvalidOperationException("Could not find the matching configuration for storage container: " + storage);
                }
            }


            return output;
        }

        private static async Task<(CatalogIndexScanResult IndexResult, CatalogPageScanResult? PageResult, HashSet<ConfiguredStorage> Intermediate, HashSet<ConfiguredStorage> Persistent)> RunDriverAsync(
            TestExecution testExecution,
            ICatalogScanDriver driver,
            CatalogScanDriverType driverType,
            HashSet<ConfiguredStorage> existingContainers)
        {
            testExecution.Output.WriteLine("Running driver type: " + driverType);

            var scanId = StorageUtility.GenerateDescendingId();

            var indexScan = new CatalogIndexScan(driverType, scanId.ToString(), scanId.Unique)
            {
                OnlyLatestLeaves = CatalogScanService.GetOnlyLatestLeavesSupport(driverType).GetValueOrDefault(true),
                Min = DateTimeOffset.Parse("2019-02-04T10:17:53.4035243Z", CultureInfo.InvariantCulture) - TimeSpan.FromTicks(1),
                Max = DateTimeOffset.Parse("2019-02-04T10:17:53.4035243Z", CultureInfo.InvariantCulture),
            };

            var pageScan = new CatalogPageScan(indexScan.StorageSuffix, indexScan.ScanId, "page")
            {
                DriverType = driverType,
                OnlyLatestLeaves = indexScan.OnlyLatestLeaves,
                Min = indexScan.Min,
                Max = indexScan.Max,
                BucketRanges = indexScan.BucketRanges,
                Url = "https://api.nuget.org/v3/catalog0/page7967.json",
                Rank = 7967,
            };

            var leafScan = new CatalogLeafScan(pageScan.StorageSuffix, pageScan.ScanId, pageScan.PageId, "leaf")
            {
                DriverType = driverType,
                Min = pageScan.Min,
                Max = pageScan.Max,
                BucketRanges = pageScan.BucketRanges,
                PageUrl = pageScan.Url,
                CommitTimestamp = indexScan.Max,
                CommitId = "c9c0205b-210b-4540-8ac7-bea3fa61f455",
                LeafType = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2019.02.04.10.17.53/dotnet-sos.1.0.0.json",
                PackageId = "dotnet-sos",
                PackageVersion = "1.0.0",
            };

            var intermediateContainers = new HashSet<ConfiguredStorage>();
            async Task UpdateIntermediateContainers()
            {
                intermediateContainers.UnionWith(await GetContainersAsync(testExecution));
            }

            await driver.InitializeAsync(indexScan);

            await UpdateIntermediateContainers();

            var indexResult = await driver.ProcessIndexAsync(indexScan);

            await UpdateIntermediateContainers();

            CatalogPageScanResult? pageResult = null;
            if (indexResult == CatalogIndexScanResult.ExpandAllLeaves)
            {
                pageResult = await driver.ProcessPageAsync(pageScan);

                await UpdateIntermediateContainers();
            }

            if (pageResult != CatalogPageScanResult.Processed)
            {
                if (driver is ICatalogLeafScanBatchDriver batchDriver)
                {
                    var leafResult = await batchDriver.ProcessLeavesAsync(new[] { leafScan });
                    if (leafResult.Failed.Count > 0 || leafResult.TryAgainLater.Count > 0)
                    {
                        throw new InvalidOperationException($"Batch driver {driverType} did not succeed.");
                    }
                }
                else if (driver is ICatalogLeafScanNonBatchDriver nonBatchDriver)
                {
                    var leafResult = await nonBatchDriver.ProcessLeafAsync(leafScan);
                    if (leafResult.Type != DriverResultType.Success)
                    {
                        throw new InvalidOperationException($"Non-batch driver {driverType} did not succeed.");
                    }
                }
                else
                {
                    throw new NotImplementedException("No leaf-based driver interface was found.");
                }

                await UpdateIntermediateContainers();
            }

            await driver.StartAggregateAsync(indexScan);

            await UpdateIntermediateContainers();

            if (!await driver.IsAggregateCompleteAsync(indexScan))
            {
                await testExecution.ProcessQueueAsync(
                    m => Task.FromResult(true),
                    () => driver.IsAggregateCompleteAsync(indexScan));
            }

            await UpdateIntermediateContainers();

            await driver.FinalizeAsync(indexScan);

            var persistentContainers = (await GetContainersAsync(testExecution)).ToHashSet();
            persistentContainers.ExceptWith(existingContainers);

            intermediateContainers.ExceptWith(existingContainers);
            intermediateContainers.ExceptWith(persistentContainers);

            return (indexResult, pageResult, intermediateContainers, persistentContainers);
        }

        public static IEnumerable<object[]> DriverTypeTestData => CatalogScanCursorService
            .StartableDriverTypes
            .OrderBy(x => x.ToString(), StringComparer.Ordinal)
            .Select(x => new object[] { x });

        public static IReadOnlyList<string> DriverNames => CatalogScanCursorService
            .StartableDriverTypes
            .Select(x => x.ToString())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        public static IEnumerable<object[]> DriverNameTestData => DriverNames.Select(x => new object[] { x });

        private class TestExecution : BaseWorkerLogicIntegrationTest
        {
            public TestExecution(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        private readonly ITestOutputHelper _output;
        private readonly DefaultWebApplicationFactory<StaticFilesStartup> _factory;

        public DriverDocsTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
        {
            _output = output;
            _factory = factory;
        }
    }
}
