// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kusto.Cloud.Platform.Data;
using Kusto.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionWithRealKustoTest : BaseWorkerLogicIntegrationTest
    {
        public string Dir = nameof(KustoIngestionWithRealKustoTest);

        /// <summary>
        /// If this fails with Kusto permissions, use a command like this:
        /// For MSA users: .add database DB_NAME admins('msauser=MSA_EMAIL')
        /// For AAD users: .add database DB_NAME admins('aaduser=AAD_USER_OBJECT_ID;AAD_TENANT_ID')
        /// For AAD app registrations: .add database DB_NAME admins('aadapp=AAD_CLIENT_ID;AAD_TENANT_ID')
        /// </summary>
        [KustoFact]
        public async Task ExecuteAsync()
        {
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultStorageBucketCount = 1;
                x.KustoTableNameFormat = StoragePrefix + "_{0}";
                x.KustoConnectionString = TestSettings.KustoConnectionString;
                x.KustoDatabaseName = TestSettings.KustoDatabaseName;
                x.KustoClientCertificateContent = TestSettings.KustoClientCertificateContent;
            };

            // Arrange
            var min0 = DateTimeOffset.Parse("2018-12-31T21:16:31.1342711Z");
            var max1 = DateTimeOffset.Parse("2018-12-31T21:22:25.1269269Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.CatalogDataToCsv, min0);
            await UpdateAsync(CatalogScanDriverType.CatalogDataToCsv, max1);
            await KustoIngestionService.InitializeAsync();

            // Act
            var ingestion = await KustoIngestionService.StartAsync();
            ingestion = await UpdateAsync(ingestion);

            // Assert
            var expectedContainers = new[]
            {
                Options.Value.CatalogLeafItemContainerName,
                Options.Value.PackageDeprecationContainerName,
                Options.Value.PackageVulnerabilityContainerName,
            };

            var tables = await GetKustoTablesAsync();
            Assert.Equal(expectedContainers.Length, tables.Count);

            foreach (var table in tables)
            {
                (var defaultTableName, var data) = await GetTableData(expectedContainers, table);
                var actual = SerializeTestJson(data);
                var testDataFile = Path.Combine(TestData, Dir, defaultTableName + ".json");
                AssertEqualWithDiff(testDataFile, actual);
            }
        }

        private async Task<(string DefaultTableName, List<string[]> Data)> GetTableData(IEnumerable<string> expectedContainers, string table)
        {
            var containers = Host.Services.GetRequiredService<CsvResultStorageContainers>();
            var tableToDefaultTable = expectedContainers.ToDictionary(
                x => containers.GetKustoTableName(x),
                x => containers.GetDefaultKustoTableName(x));

            var queryProvider = Host.Services.GetRequiredService<ICslQueryProvider>();
            using var reader = await queryProvider.ExecuteQueryAsync(
                TestSettings.KustoDatabaseName,
                table,
                new ClientRequestProperties());

            Assert.Contains(table, tableToDefaultTable.Keys.ToList());
            var defaultTableName = tableToDefaultTable[table];

            var data = reader.ToStringColumns(includeHeaderAsFirstRow: true).ToList();
            var headers = data[0];
            data.RemoveAt(0);
            data.Sort((a, b) => string.CompareOrdinal(string.Join(",", a), string.Join(",", b)));
            data.Insert(0, headers);

            return (defaultTableName, data);
        }

        protected override async Task DisposeInternalAsync()
        {
            if (new KustoFactAttribute().Skip is null)
            {
                await CleanUpKustoTablesAsync();
            }

            await base.DisposeInternalAsync();
        }

        public KustoIngestionWithRealKustoTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
