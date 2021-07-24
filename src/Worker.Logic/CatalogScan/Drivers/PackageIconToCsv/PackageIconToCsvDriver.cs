// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    public class PackageIconToCsvDriver : ICatalogLeafToCsvDriver<PackageIcon>, ICsvResultStorage<PackageIcon>
    {
        private static IReadOnlyList<string> IgnoredAttributes = new[] { "date:create", "date:modify", "signature" };

        private readonly CatalogClient _catalogClient;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<PackageIconToCsvDriver> _logger;

        public PackageIconToCsvDriver(
            CatalogClient catalogClient,
            FlatContainerClient flatContainerClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<PackageIconToCsvDriver> logger)
        {
            _catalogClient = catalogClient;
            _flatContainerClient = flatContainerClient;
            _options = options;
            _logger = logger;
        }

        public string ResultContainerName => _options.Value.PackageIconContainerName;
        public bool SingleMessagePerId => false;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<CsvRecordSet<PackageIcon>>> ProcessLeafAsync(CatalogLeafItem item, int attemptCount)
        {
            (var resultType, var records) = await ProcessLeafInternalAsync(item);
            if (resultType == TempStreamResultType.SemaphoreNotAvailable)
            {
                return DriverResult.TryAgainLater<CsvRecordSet<PackageIcon>>();
            }

            return DriverResult.Success(new CsvRecordSet<PackageIcon>(PackageRecord.GetBucketKey(item), records));
        }

        public async Task<(TempStreamResultType, List<PackageIcon>)> ProcessLeafInternalAsync(CatalogLeafItem item)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (item.Type == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                return (
                    TempStreamResultType.Success,
                    new List<PackageIcon> { new PackageIcon(scanId, scanTimestamp, leaf) }
                );
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                (var contentType, var result) = await _flatContainerClient.DownloadPackageIconToFileAsync(
                    item.PackageId,
                    item.PackageVersion,
                    CancellationToken.None);
                using (result)
                {
                    if (result == null)
                    {
                        return (
                            TempStreamResultType.Success,
                            new List<PackageIcon> { new PackageIcon(scanId, scanTimestamp, leaf) { ResultType = PackageIconResultType.NoIcon } }
                        );
                    }

                    if (result.Type == TempStreamResultType.SemaphoreNotAvailable)
                    {
                        return (
                            TempStreamResultType.SemaphoreNotAvailable,
                            null
                        );
                    }

                    var output = new PackageIcon(scanId, scanTimestamp, leaf)
                    {
                        ResultType = PackageIconResultType.Available,
                        FileSize = result.Stream.Length,
                        MD5 = result.Hash.MD5.ToBase64(),
                        SHA1 = result.Hash.SHA1.ToBase64(),
                        SHA256 = result.Hash.SHA256.ToBase64(),
                        SHA512 = result.Hash.SHA512.ToBase64(),
                        ContentType = contentType,
                    };

                    try
                    {
                        using var collection = GetMagickImageCollection(leaf, result);
                        using var image = collection.First();
                        var attributeNames = image
                            .AttributeNames
                            .Except(IgnoredAttributes)
                            .OrderBy(x => x)
                            .ToList();
                        output.Format = image.Format.ToString();
                        output.Width = image.Width;
                        output.Height = image.Height;
                        output.FrameCount = collection.Count;
                        output.IsOpaque = image.IsOpaque;
                        output.Signature = image.Signature;
                        output.AttributeNames = JsonConvert.SerializeObject(attributeNames);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could to process image for {Id}/{Version}.", leaf.PackageId, leaf.PackageVersion);
                        output.ResultType = PackageIconResultType.Error;
                    }

                    return (TempStreamResultType.Success, new List<PackageIcon> { output });
                }
            }
        }

        private MagickImageCollection GetMagickImageCollection(CatalogLeaf leaf, TempStreamResult result)
        {
            // Try to detect the format. ImageMagick appears to not detect .ico files when only given a stream.
            result.Stream.Position = 0;
            var format = FormatDetector.Detect(result.Stream);

            if (format != MagickFormat.Unknown)
            {
                try
                {
                    result.Stream.Position = 0;
                    return new MagickImageCollection(result.Stream, format);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "ImageMagick failed to open the icon for {Id}/{Version} with format {Format}.",
                        leaf.PackageId,
                        leaf.PackageVersion,
                        format);
                }
            }

            result.Stream.Position = 0;
            return new MagickImageCollection(result.Stream);
        }

        public List<PackageIcon> Prune(List<PackageIcon> records)
        {
            return PackageRecord.Prune(records);
        }

        public Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(PackageIcon record)
        {
            throw new NotImplementedException();
        }
    }
}
