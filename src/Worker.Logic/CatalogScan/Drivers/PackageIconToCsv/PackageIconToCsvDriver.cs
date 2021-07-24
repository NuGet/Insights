// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    public class PackageIconToCsvDriver : ICatalogLeafToCsvDriver<PackageIcon>, ICsvResultStorage<PackageIcon>
    {
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
                        using var image = new Bitmap(result.Stream);
                        var propertyItems = JsonConvert.SerializeObject(image.PropertyItems.Select(x => new { x.Id, x.Type }));
                        output.Format = image.RawFormat.ToString();
                        output.Width = image.Width;
                        output.Height = image.Height;
                        output.FrameCountByTime = TryGet(() => image.GetFrameCount(FrameDimension.Time));
                        output.FrameCountByResolution = TryGet(() => image.GetFrameCount(FrameDimension.Resolution));
                        output.FrameCountByPage = TryGet(() => image.GetFrameCount(FrameDimension.Page));
                        output.HorizontalResolution = image.HorizontalResolution;
                        output.VerticalResolution = image.VerticalResolution;
                        output.Flags = image.Flags;
                        output.PixelFormat = image.PixelFormat.ToString();
                        output.PropertyItems = propertyItems;
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

        private T? TryGet<T>(Func<T> get) where T : struct
        {
            try
            {
                return get();
            }
            catch (ExternalException)
            {
                return default;
            }
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
