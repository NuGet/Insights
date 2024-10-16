// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using ImageMagick;

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    public class PackageIconToCsvDriver : ICatalogLeafToCsvDriver<PackageIcon>, ICsvResultStorage<PackageIcon>
    {
        private static readonly IReadOnlyList<string> IgnoredAttributes = ["date:create", "date:modify", "signature"];

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

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<IReadOnlyList<PackageIcon>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            (var resultType, var records) = await ProcessLeafInternalAsync(leafScan);
            if (resultType == TempStreamResultType.SemaphoreNotAvailable)
            {
                return DriverResult.TryAgainLater<IReadOnlyList<PackageIcon>>();
            }

            return DriverResult.Success(records);
        }

        public async Task<(TempStreamResultType, IReadOnlyList<PackageIcon>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);
                return (
                    TempStreamResultType.Success,
                    [new PackageIcon(scanId, scanTimestamp, leaf)]
                );
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var result = await _flatContainerClient.DownloadPackageIconToFileAsync(
                    leafScan.PackageId,
                    leafScan.PackageVersion,
                    CancellationToken.None);

                if (result is null)
                {
                    return (
                        TempStreamResultType.Success,
                        [new PackageIcon(scanId, scanTimestamp, leaf) { ResultType = PackageIconResultType.NoIcon }]
                    );
                }

                await using (result.Value.Body)
                {
                    if (result.Value.Body.Type == TempStreamResultType.SemaphoreNotAvailable)
                    {
                        return (
                            TempStreamResultType.SemaphoreNotAvailable,
                            null
                        );
                    }

                    var output = new PackageIcon(scanId, scanTimestamp, leaf)
                    {
                        ResultType = PackageIconResultType.Available,
                        FileLength = result.Value.Body.Stream.Length,
                        FileSHA256 = result.Value.Body.Hash.SHA256.ToBase64(),
                        ContentType = result.Value.ContentType,
                    };

                    // Try to detect the format. ImageMagick appears to not detect .ico files when only given a stream.
                    result.Value.Body.Stream.Position = 0;
                    var format = FormatDetector.Detect(result.Value.Body.Stream);
                    output.HeaderFormat = format.ToString();

                    try
                    {
                        (var autoDetectedFormat, var frames) = GetMagickImageCollection(leaf, result.Value.Body, format);
                        using (frames)
                        {
                            using var image = frames.First();

                            // Maintain original (frame) order of formats and dimensions
                            var frameFormats = new List<string>();
                            var frameDimensions = new List<object>();
                            var uniqueFrameFormats = new HashSet<string>();
                            var uniqueFrameDimensions = new HashSet<(long, long)>();

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
                            output.FrameFormats = KustoDynamicSerializer.Serialize(frameFormats);
                            output.FrameDimensions = KustoDynamicSerializer.Serialize(frameDimensions);
                            output.FrameAttributeNames = KustoDynamicSerializer.Serialize(frameAttributeNames.Except(IgnoredAttributes).OrderBy(x => x, StringComparer.Ordinal).ToList());
                        }
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        _logger.LogWarning(ex, "Failed to process icon for {Id}/{Version}.", leaf.PackageId, leaf.PackageVersion);
                        output.ResultType = PackageIconResultType.Error;
                    }

                    return (TempStreamResultType.Success, [output]);
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
    }
}
