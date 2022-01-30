// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    public class PackageIconToCsvDriver : ICatalogLeafToCsvDriver<PackageIcon>, ICsvResultStorage<PackageIcon>
    {
        private static readonly IReadOnlyList<string> IgnoredAttributes = new[] { "date:create", "date:modify", "signature" };

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

        public async Task<DriverResult<CsvRecordSet<PackageIcon>>> ProcessLeafAsync(ICatalogLeafItem item, int attemptCount)
        {
            (var resultType, var records) = await ProcessLeafInternalAsync(item);
            if (resultType == TempStreamResultType.SemaphoreNotAvailable)
            {
                return DriverResult.TryAgainLater<CsvRecordSet<PackageIcon>>();
            }

            return DriverResult.Success(new CsvRecordSet<PackageIcon>(PackageRecord.GetBucketKey(item), records));
        }

        public async Task<(TempStreamResultType, List<PackageIcon>)> ProcessLeafInternalAsync(ICatalogLeafItem item)
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

                    // Try to detect the format. ImageMagick appears to not detect .ico files when only given a stream.
                    result.Stream.Position = 0;
                    var format = FormatDetector.Detect(result.Stream);
                    output.HeaderFormat = format.ToString();

                    try
                    {
                        (var autoDetectedFormat, var frames) = GetMagickImageCollection(leaf, result, format);
                        using (frames)
                        {
                            using var image = frames.First();

                            // Maintain original (frame) order of formats and dimensions
                            var frameFormats = new List<string>();
                            var frameDimensions = new List<object>();
                            var uniqueFrameFormats = new HashSet<string>();
                            var uniqueFrameDimensions = new HashSet<(int, int)>();

                            var frameAttributeNames = new HashSet<string>();
                            foreach (var frame in frames)
                            {
                                var frameFormat = frame.Format.ToString();
                                if (uniqueFrameFormats.Add(frameFormat))
                                {
                                    frameFormats.Add(frameFormat);
                                }

                                if (uniqueFrameDimensions.Add((frame.Width, frame.Height)))
                                {
                                    frameDimensions.Add(new { frame.Width, frame.Height });
                                }

                                foreach (var attributeName in frame.AttributeNames)
                                {
                                    frameAttributeNames.Add(attributeName);
                                }
                            }

                            output.Signature = ByteArrayExtensions.StringToByteArray(image.Signature).ToBase64();
                            output.AutoDetectedFormat = autoDetectedFormat;
                            output.Width = image.Width;
                            output.Height = image.Height;
                            output.IsOpaque = image.IsOpaque;
                            output.FrameCount = frames.Count;
                            output.FrameFormats = JsonSerializer.Serialize(frameFormats);
                            output.FrameDimensions = JsonSerializer.Serialize(frameDimensions);
                            output.FrameAttributeNames = JsonSerializer.Serialize(frameAttributeNames.Except(IgnoredAttributes).OrderBy(x => x).ToList());
                        }
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        _logger.LogWarning(ex, "Failed to process icon for {Id}/{Version}.", leaf.PackageId, leaf.PackageVersion);
                        output.ResultType = PackageIconResultType.Error;
                    }

                    return (TempStreamResultType.Success, new List<PackageIcon> { output });
                }
            }
        }

        private (bool, MagickImageCollection) GetMagickImageCollection(CatalogLeaf leaf, TempStreamResult result, MagickFormat format)
        {
            try
            {
                result.Stream.Position = 0;
                return (true, new MagickImageCollection(result.Stream));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _logger.LogInformation(
                    ex,
                    "ImageMagick failed to auto-detect format of icon for {Id}/{Version}.",
                    leaf.PackageId,
                    leaf.PackageVersion);
            }

            result.Stream.Position = 0;
            return (false, new MagickImageCollection(result.Stream, format));
        }

        public List<PackageIcon> Prune(List<PackageIcon> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public Task<ICatalogLeafItem> MakeReprocessItemOrNullAsync(PackageIcon record)
        {
            throw new NotImplementedException();
        }
    }
}
