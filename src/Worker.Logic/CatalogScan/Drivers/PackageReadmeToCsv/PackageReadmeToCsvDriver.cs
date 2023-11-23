// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.PackageReadmeToCsv
{
    public class PackageReadmeToCsvDriver : ICatalogLeafToCsvDriver<PackageReadme>, ICsvResultStorage<PackageReadme>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageReadmeService _packageReadmeService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageReadmeToCsvDriver(
            CatalogClient catalogClient,
            PackageReadmeService packageReadmeService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _packageReadmeService = packageReadmeService;
            _options = options;
        }

        public string ResultContainerName => _options.Value.PackageReadmeContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageReadme> Prune(List<PackageReadme> records, bool isFinalPrune)
        {
            return PackageRecord.Prune(records, isFinalPrune);
        }

        public async Task InitializeAsync()
        {
            await _packageReadmeService.InitializeAsync();
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<CsvRecordSet<PackageReadme>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var records = await ProcessLeafInternalAsync(leafScan);
            return DriverResult.Success(new CsvRecordSet<PackageReadme>(PackageRecord.GetBucketKey(leafScan), records));
        }

        private async Task<List<PackageReadme>> ProcessLeafInternalAsync(CatalogLeafScan leafItem)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafItem.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafItem.LeafType, leafItem.Url);
                return new List<PackageReadme> { new PackageReadme(scanId, scanTimestamp, leaf) };
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafItem.LeafType, leafItem.Url);
                var info = await _packageReadmeService.GetOrUpdateInfoFromLeafItemAsync(leafItem.ToPackageIdentityCommit());
                return new List<PackageReadme> { GetRecord(scanId, scanTimestamp, leaf, info) };
            }
        }

        private PackageReadme GetRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, PackageReadmeService.PackageReadmeInfoV1 info)
        {
            PackageReadmeResultType resultType;
            switch (info.ReadmeType)
            {
                case ReadmeType.None:
                    return new PackageReadme(scanId, scanTimestamp, leaf)
                    {
                        ResultType = PackageReadmeResultType.None,
                    };
                case ReadmeType.Legacy:
                    resultType = PackageReadmeResultType.Legacy;
                    break;
                case ReadmeType.Embedded:
                    resultType = PackageReadmeResultType.Embedded;
                    break;
                default:
                    throw new NotImplementedException();
            }

            DateTimeOffset? lastModified = null;
            var lastModifiedString = info.HttpHeaders["Last-Modified"].FirstOrDefault();
            if (lastModifiedString is not null)
            {
                lastModified = DateTimeOffset.Parse(lastModifiedString, CultureInfo.InvariantCulture);
            }

            using var hasher = SHA256.Create();
            var sha256 = hasher.ComputeHash(info.ReadmeBytes.AsStream()).ToBase64();

            using var reader = new StreamReader(info.ReadmeBytes.AsStream());
            var content = reader.ReadToEnd();

            var record = new PackageReadme(scanId, scanTimestamp, leaf)
            {
                ResultType = resultType,
                Size = info.ReadmeBytes.Length,
                LastModified = lastModified,
                SHA256 = sha256,
                Content = content,
            };

            return record;
        }
    }
}
