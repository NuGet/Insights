// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO.Compression;
using NuGet.Insights.Worker.PackageAssetToCsv;
using NuGet.ContentModel;
using System.Buffers;
using System.Security.Cryptography;
using Knapcode.MiniZip;

namespace NuGet.Insights.Worker.PackageContentToCsv
{
    public class PackageContentToCsvDriver : ICatalogLeafToCsvDriver<PackageContent>, ICsvResultStorage<PackageContent>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly FileDownloader _fileDownloader;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<PackageContentToCsvDriver> _logger;
        private readonly IReadOnlyDictionary<string, int> _extensionToOrder;

        public PackageContentToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            FlatContainerClient flatContainerClient,
            FileDownloader fileDownloader,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<PackageContentToCsvDriver> logger)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _flatContainerClient = flatContainerClient;
            _fileDownloader = fileDownloader;

            var extensionToOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var extension in options.Value.PackageContentFileExtensions)
            {
                if (extensionToOrder.ContainsKey(extension))
                {
                    continue;
                }

                extensionToOrder.Add(extension, extensionToOrder.Count + 1);
            }

            _extensionToOrder = extensionToOrder;
            _options = options;
            _logger = logger;
        }

        public string ResultContainerName => _options.Value.PackageContentContainerName;
        public bool SingleMessagePerId => false;

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<CsvRecordSet<PackageContent>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                return MakeResults(leafScan, new List<PackageContent> { new PackageContent(scanId, scanTimestamp, leaf) });
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var zipDirectory = await _packageFileService.GetZipDirectoryAsync(leafScan.ToPackageIdentityCommit());
                if (zipDirectory == null)
                {
                    return MakeEmptyResults(leaf);
                }

                if (!zipDirectory.Entries.Any(e => GetExtensionAndOrder(e.GetName()).Order.HasValue))
                {
                    return MakeNoContentResults(scanId, scanTimestamp, leaf);
                }

                var url = await _flatContainerClient.GetPackageContentUrlAsync(leafScan.PackageId, leafScan.PackageVersion);
                var result = await _fileDownloader.DownloadUrlToFileAsync(url, CancellationToken.None);

                if (result is null)
                {
                    return MakeEmptyResults(leaf);
                }

                using (result.Value.Body)
                {
                    if (result.Value.Body.Type == TempStreamResultType.SemaphoreNotAvailable)
                    {
                        return DriverResult.TryAgainLater<CsvRecordSet<PackageContent>>();
                    }

                    using var zipArchive = new ZipArchive(result.Value.Body.Stream);

                    var recognizedAssets = new HashSet<string>();

                    var groups = new List<ContentItemGroup>();
                    var contentItemCollection = new ContentItemCollection();
                    contentItemCollection.Load(zipArchive.Entries.Select(e => e.FullName));
                    foreach (var pair in PackageAssetToCsvDriver.GetPatternSets())
                    {
                        try
                        {
                            contentItemCollection.PopulateItemGroups(pair.Value, groups);
                        }
                        catch (ArgumentException ex) when (PackageAssetToCsvDriver.IsInvalidDueToHyphenInProfile(ex))
                        {
                            _logger.LogWarning(ex, "Package {Id} {Version} contains a portable framework with a hyphen in the profile.", leaf.PackageId, leaf.PackageVersion);
                        }

                        recognizedAssets.UnionWith(groups.SelectMany(g => g.Items.Select(i => i.Path)));
                        groups.Clear();
                    }

                    var filteredEntries = zipArchive
                        .Entries
                        .Select((e, i) => (Entry: e, ExtensionAndOrder: GetExtensionAndOrder(e.FullName), SequenceNumber: i))
                        .Where(e => e.ExtensionAndOrder.Order.HasValue)
                        .OrderBy(e => e.ExtensionAndOrder.Order) // use the configured order
                        .ThenByDescending(e => recognizedAssets.Contains(e.Entry.FullName)) // prefer recognized assets
                        .ThenBy(e => e.SequenceNumber) // prefer items at the beginning of the zip entry listing
                        .ToList();

                    var records = new List<PackageContent>();
                    var addedHashes = new HashSet<string>();
                    var remainingBytes = _options.Value.PackageContentMaxSizePerPackage;
                    foreach (var entry in filteredEntries)
                    {
                        var record = new PackageContent(scanId, scanTimestamp, leaf, PackageContentResultType.AllLoaded)
                        {
                            Path = entry.Entry.FullName,
                            FileExtension = entry.ExtensionAndOrder.Extension,
                            SequenceNumber = entry.SequenceNumber,
                        };

                        using var entryStream = entry.Entry.Open();
                        using var sha256 = SHA256.Create();
                        using var cryptoStream = new CryptoStream(entryStream, sha256, CryptoStreamMode.Read);
                        var limitBytes = Math.Min(_options.Value.PackageContentMaxSizePerFile, remainingBytes);
                        using var limitStream = new LimitStream(cryptoStream, limitBytes);
                        using var streamReader = new StreamReader(limitStream);
                        var content = streamReader.ReadToEnd();
                        var size = limitStream.ReadBytes;

                        record.Truncated = limitStream.Truncated;
                        record.TruncatedSize = limitStream.Truncated ? size : null;

                        if (limitStream.Truncated)
                        {
                            // The limit stream reads one additional byte to detect that the data was indeed truncated.
                            size++;

                            var buffer = ArrayPool<byte>.Shared.Rent(1024);
                            try
                            {
                                int read;
                                while ((read = cryptoStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    size += read;
                                }
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }

                        record.Size = size;
                        record.SHA256 = sha256.Hash.ToBase64();

                        if (addedHashes.Add(record.SHA256))
                        {
                            record.Content = content.Length > 0 ? content : null;
                            remainingBytes = Math.Max(0, remainingBytes - size);
                            record.DuplicateContent = false;
                        }
                        else
                        {
                            record.DuplicateContent = true;
                        }

                        records.Add(record);
                    }

                    if (!records.Any())
                    {
                        throw new InvalidOperationException("There should be at least one matched entry.");
                    }

                    if (records.Any(r => r.Truncated == true))
                    {
                        foreach (var record in records)
                        {
                            record.ResultType = PackageContentResultType.PartiallyLoaded;
                        }
                    }

                    return MakeResults(leafScan, records);
                }
            }
        }

        private (string Extension, int? Order) GetExtensionAndOrder(string path)
        {
            int? order = null;
            string extension = null;
            foreach (var pair in _extensionToOrder)
            {
                if (pair.Key.Length == 0 && Path.GetExtension(path).Length == 0)
                {
                    if (!order.HasValue || pair.Value < order)
                    {
                        order = pair.Value;
                        extension = pair.Key;
                    }
                }
                else if (path.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    if (!order.HasValue || pair.Value < order)
                    {
                        order = pair.Value;
                        extension = pair.Key;
                    }
                }
            }

            return (extension, order);
        }

        private static DriverResult<CsvRecordSet<PackageContent>> MakeNoContentResults(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return MakeResults(leaf, new List<PackageContent> { new PackageContent(scanId, scanTimestamp, leaf, PackageContentResultType.NoContent) });
        }

        private static DriverResult<CsvRecordSet<PackageContent>> MakeEmptyResults(PackageDetailsCatalogLeaf leaf)
        {
            // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted record.
            return MakeResults(leaf, new List<PackageContent>());
        }

        private static DriverResult<CsvRecordSet<PackageContent>> MakeResults(ICatalogLeafItem item, List<PackageContent> records)
        {
            return DriverResult.Success(new CsvRecordSet<PackageContent>(PackageRecord.GetBucketKey(item), records));
        }

        public List<PackageContent> Prune(List<PackageContent> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }
    }
}
