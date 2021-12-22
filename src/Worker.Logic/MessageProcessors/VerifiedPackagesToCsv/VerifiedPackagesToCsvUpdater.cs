// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.VerifiedPackagesToCsv
{
    public class VerifiedPackagesToCsvUpdater : IAuxiliaryFileUpdater<AsOfData<string>>
    {
        private readonly VerifiedPackagesClient _client;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public VerifiedPackagesToCsvUpdater(
            VerifiedPackagesClient client,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _client = client;
            _options = options;
        }

        public string OperationName => "VerifiedPackagesToCsv";
        public string BlobName => "verified-packages";
        public string ContainerName => _options.Value.PackageOwnerContainerName;
        public TimeSpan Frequency => _options.Value.OwnersToCsvFrequency;
        public bool HasRequiredConfiguration => _options.Value.OwnersV2Url != null;
        public bool AutoStart => _options.Value.AutoStartOwnersToCsv;
        public Type RecordType => typeof(VerifiedPackageRecord);

        public async Task<AsOfData<string>> GetDataAsync()
        {
            return await _client.GetVerifiedPackageSetAsync();
        }

        public async Task WriteAsync(IVersionSet versionSet, AsOfData<string> data, StreamWriter writer)
        {
            var record = new VerifiedPackageRecord { AsOfTimestamp = data.AsOfTimestamp };
            record.WriteHeader(writer);

            var verifiedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await foreach (var packageId in data.Entries)
            {
                if (!versionSet.DidIdEverExist(packageId))
                {
                    continue;
                }

                verifiedPackageIds.Add(packageId);
            }

            foreach (var packageId in verifiedPackageIds)
            {
                record.LowerId = packageId.ToLowerInvariant();
                record.Id = packageId;
                record.IsVerified = true;
                record.Write(writer);
            }

            // Add IDs that are not mentioned in the data and therefore are not verified. This makes joins on the
            // produced data set easier.
            foreach (var id in versionSet.GetUncheckedIds())
            {
                record.LowerId = id.ToLowerInvariant();
                record.Id = id;
                record.IsVerified = false;
                record.Write(writer);
            }
        }
    }
}
