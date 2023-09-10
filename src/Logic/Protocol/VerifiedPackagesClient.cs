// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

#nullable enable

namespace NuGet.Insights
{
    public class VerifiedPackagesClient
    {
        private readonly BlobStorageJsonClient _storageClient;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public VerifiedPackagesClient(
            BlobStorageJsonClient storageClient,
            IOptions<NuGetInsightsSettings> options)
        {
            _storageClient = storageClient;
            _options = options;
        }

        public async Task<AsOfData<VerifiedPackage>> GetAsync()
        {
            return await _storageClient.DownloadNewestAsync(
                _options.Value.VerifiedPackagesV1Urls,
                _options.Value.VerifiedPackagesV1AgeLimit,
                "verifiedPackages.json",
                DeserializeAsync);
        }

        private IAsyncEnumerable<VerifiedPackage> DeserializeAsync(Stream stream)
        {
            return JsonSerializer
                .DeserializeAsyncEnumerable<string>(stream)
                .Select(x => new VerifiedPackage(x));
        }
    }
}
