// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Kusto.Data.Net.Client;
using Kusto.Data;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Insights.Worker.DownloadsToCsv;
using Kusto.Data.Common;
using Microsoft.Extensions.Logging;
using NuGet.Insights.Worker.BuildVersionSet;
using MessagePack;
using System.Diagnostics;

namespace NuGet.Insights.Tool
{
    public class IngestDownloadsJsonCommand : ICommand
    {
        private static readonly string PackageTimestampQueryFormat = @$"
{{0}}{KustoDDL.PackageVersionRecordDefaultTableName}
| project Created, Id, Version, Identity
| join kind=inner (
    {{0}}{KustoDDL.CatalogLeafItemRecordDefaultTableName}
    | summarize FirstTimestamp = min(CommitTimestamp) by Identity
) on Identity
| extend Created = iff(isempty(Created), FirstTimestamp, Created)
| project Created, Id, Version
| where Created >= datetime({{1:O}})
| order by Created asc
| take {{2}}
";

        private readonly ILogger<IngestDownloadsJsonCommand> _logger;

        private CommandOption<string> _kustoConnectionString;
        private CommandOption<string> _kustoDatabase;
        private CommandOption<string> _kustoTablePrefix;

        private CommandOption<string> _inStorageAccount;
        private CommandOption<string> _inContainer;
        private CommandOption<string> _inSas;
        private CommandOption<string> _inPrefix;
        private CommandOption<string> _inBlobName;
        private CommandOption<bool> _inSnapshots;

        private CommandOption<string> _outStorageAccount;
        private CommandOption<string> _outContainer;
        private CommandOption<string> _outSas;

        public IngestDownloadsJsonCommand(ILogger<IngestDownloadsJsonCommand> logger)
        {
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
            _kustoConnectionString = app.Option<string>(
                "--kusto-connection-string",
                "connection string for Kusto",
                CommandOptionType.SingleValue,
                o => o.IsRequired());
            _kustoDatabase = app.Option<string>(
                "--kusto-database",
                "Kusto database name",
                CommandOptionType.SingleValue,
                o => o.IsRequired());
            _kustoTablePrefix = app.Option<string>(
                "--kusto-table-prefix",
                "prefix for all of the NuGet.Insights table names",
                CommandOptionType.SingleValue);

            _inStorageAccount = app.Option<string>(
                "--in-storage-account",
                "name of the Azure storage account",
                CommandOptionType.SingleValue,
                o => o.IsRequired());
            _inContainer = app.Option<string>(
                "--in-container",
                "name of the blob container",
                CommandOptionType.SingleValue,
                o => o.IsRequired());
            _inSas = app.Option<string>(
                "--in-sas",
                "the SAS token to use to authenticate with Azure storage",
                CommandOptionType.SingleValue,
                o => o.IsRequired());
            _inPrefix = app.Option<string>(
                "--in-prefix",
                "the prefix used to filter blob names",
                CommandOptionType.SingleValue);
            _inBlobName = app.Option<string>(
                "--in-blob",
                "the name of the blob",
                CommandOptionType.SingleValue);
            _inSnapshots = app.Option<bool>(
                "--in-snapshots",
                "whether or not to enumerate blob snapshots",
                CommandOptionType.NoValue);

            _outStorageAccount = app.Option<string>(
                "--out-storage-account",
                "name of the Azure storage account",
                CommandOptionType.SingleValue,
                o => o.IsRequired());
            _outContainer = app.Option<string>(
                "--out-container",
                "name of the blob container",
                CommandOptionType.SingleValue,
                o => o.IsRequired());
            _outSas = app.Option<string>(
                "--out-sas",
                "the SAS token to use to authenticate with Azure storage",
                CommandOptionType.SingleValue,
            o => o.IsRequired());
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var versionSet = await GetOrSavePackageHistoryAsync();

            var outputContainerClient = new BlobContainerClient(
                new Uri($"https://{_outStorageAccount.ParsedValue}.blob.core.windows.net/{_outContainer.ParsedValue}"),
                new AzureSasCredential(_outSas.ParsedValue));

            var tempFilePath = $"temp-{Process.GetCurrentProcess().Id}.csv";
            using var tempFile = new FileStream(
                tempFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.DeleteOnClose);

            await foreach ((var inputBlobItem, var inputBlob) in GetBlobsAsync())
            {
                Console.WriteLine(inputBlob.Uri);
                var asOf = inputBlobItem.Properties.LastModified.Value;

                var outputBlob = outputContainerClient.GetBlobClient($"{inputBlob.AccountName}-{inputBlob.BlobContainerName}-downloads-{asOf:yyyy.MM.dd.HH.mm.ss.fffffff}.csv.gz");

                if (await outputBlob.ExistsAsync())
                {
                    Console.WriteLine(" - Already done");
                    continue;
                }

                Console.WriteLine(" - Downloading and mapping data");
                versionSet.SetCommitTimestamp(asOf);
                await SaveToStreamAsync(tempFile, versionSet, asOf, inputBlob);

                Console.WriteLine(" - Uploading data");
                await outputBlob.UploadAsync(tempFile);
                Console.WriteLine(" - Done");
            }
        }

        private async Task<HistoryVersionSet> GetOrSavePackageHistoryAsync()
        {
            const string fileName = "package-history.dat";

            if (!File.Exists(fileName))
            {
                await SavePackageHistoryAsync(fileName);
            }

            Console.WriteLine($"Reading {fileName}...");
            using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            var packageHistory = await MessagePackSerializer.DeserializeAsync<CaseInsensitiveDictionary<CaseInsensitiveDictionary<DateTimeOffset>>>(
                fileStream,
                NuGetInsightsMessagePack.OptionsWithStringIntern);

            var versionSet = new HistoryVersionSet();
            foreach ((var id, var versions) in packageHistory)
            {
                foreach ((var version, var created) in versions)
                {
                    versionSet.AddPackage(id, version, created);
                }
            }

            return versionSet;
        }

        private async Task SavePackageHistoryAsync(string fileName)
        {
            var packageHistory = new CaseInsensitiveDictionary<CaseInsensitiveDictionary<DateTimeOffset>>();

            var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(_kustoConnectionString.ParsedValue);
            var kustoQueryProvider = KustoClientFactory.CreateCslQueryProvider(kustoConnectionStringBuilder);

            const int take = 500_000;
            var lastTimestamp = DateTimeOffset.MinValue;

            _logger.LogInformation("Loading known IDs and versions...");
            var recordCount = take;
            while (recordCount == take)
            {
                var query = string.Format(
                    PackageTimestampQueryFormat,
                    _kustoTablePrefix.ParsedValue,
                    lastTimestamp,
                    take);
                using var kustoReader = await kustoQueryProvider.ExecuteQueryAsync(_kustoDatabase.ParsedValue, query, new ClientRequestProperties());
                recordCount = 0;
                while (kustoReader.Read())
                {
                    var created = new DateTimeOffset((DateTime)kustoReader["Created"], TimeSpan.Zero);
                    var id = (string)kustoReader["Id"];
                    var version = (string)kustoReader["Version"];

                    if (!packageHistory.TryGetValue(id, out var versions))
                    {
                        versions = new CaseInsensitiveDictionary<DateTimeOffset>();
                        packageHistory.Add(id, versions);
                    }

                    if (!versions.TryGetValue(version, out var existing))
                    {
                        versions.Add(version, created);
                    }
                    else if (created < existing)
                    {
                        versions[version] = created;
                    }

                    lastTimestamp = created;
                    recordCount++;
                }
                _logger.LogInformation("Loaded {Count} packages, up to {LastTimestamp:O}.", recordCount, lastTimestamp);
            }

            Console.WriteLine($"Saving {fileName}...");
            using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            await MessagePackSerializer.SerializeAsync(fileStream, packageHistory, NuGetInsightsMessagePack.Options);
        }

        private async Task SaveToStreamAsync(FileStream tempFile, IVersionSet versionSet, DateTimeOffset asOf, BlobClient blobClient)
        {
            tempFile.Position = 0;
            tempFile.SetLength(0);

            using (var gzipStream = new GZipStream(tempFile, CompressionLevel.Optimal, leaveOpen: true))
            {
                using var outputWriter = new StreamWriter(gzipStream);

                using var inputStream = await blobClient.OpenReadAsync();
                var data = PackageDownloadsClient.DeserializeAsync(inputStream);
                var record = new PackageDownloadHistoryRecord { AsOfTimestamp = asOf };
                await DownloadsToCsvUpdater.WriteAsync(versionSet, record, data, outputWriter);
            }

            tempFile.Position = 0;
        }

        private async IAsyncEnumerable<(BlobItem Item, BlobClient Client)> GetBlobsAsync()
        {
            var containerClient = new BlobContainerClient(
                new Uri($"https://{_inStorageAccount.ParsedValue}.blob.core.windows.net/{_inContainer.ParsedValue}"),
                new AzureSasCredential(_inSas.ParsedValue));

            if (_inBlobName.HasValue())
            {
                var foundBlob = false;
                await foreach (var blob in containerClient.GetBlobsAsync(
                    traits: BlobTraits.None,
                    states: _inSnapshots.ParsedValue ? BlobStates.Snapshots : BlobStates.None,
                    prefix: _inBlobName.ParsedValue))
                {
                    if (blob.Name == _inBlobName.ParsedValue)
                    {
                        foundBlob = true;
                        yield return GetBlobClient(containerClient, blob);
                    }
                    else if (foundBlob)
                    {
                        break;
                    }
                }
            }
            else
            {
                await foreach (var blob in containerClient.GetBlobsAsync(
                    traits: BlobTraits.None,
                    states: _inSnapshots.ParsedValue ? BlobStates.Snapshots : BlobStates.None,
                    prefix: _inPrefix.ParsedValue ?? string.Empty))
                {
                    yield return GetBlobClient(containerClient, blob);
                }
            }
        }

        private static (BlobItem Item, BlobClient Client) GetBlobClient(BlobContainerClient containerClient, BlobItem blob)
        {
            var blobClient = containerClient.GetBlobClient(blob.Name);
            if (blob.Snapshot is not null)
            {
                blobClient = blobClient.WithSnapshot(blob.Snapshot);
            }

            return (blob, blobClient);
        }
    }
}
