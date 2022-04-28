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
    public class VerifiedPackagesToCsvUpdater : IAuxiliaryFileUpdater<AsOfData<VerifiedPackage>>
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
        public string BlobName => "verified_packages";
        public string ContainerName => _options.Value.VerifiedPackageContainerName;
        public TimeSpan Frequency => _options.Value.VerifiedPackagesToCsvFrequency;
        public bool HasRequiredConfiguration => _options.Value.VerifiedPackagesV1Url != null;
        public bool AutoStart => _options.Value.AutoStartVerifiedPackagesToCsv;
        public Type RecordType => typeof(VerifiedPackageRecord);

        public async Task<AsOfData<VerifiedPackage>> GetDataAsync()
        {
            return await _client.GetAsync();
        }

        public async Task WriteAsync(IVersionSet versionSet, AsOfData<VerifiedPackage> data, StreamWriter writer)
        {
            var record = new VerifiedPackageRecord { AsOfTimestamp = data.AsOfTimestamp };
            record.WriteHeader(writer);

            var verifiedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await foreach (var entry in data.Entries)
            {
                if (!versionSet.DidIdEverExist(entry.Id))
                {
                    continue;
                }

                verifiedPackageIds.Add(entry.Id);
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
