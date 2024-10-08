// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class VerifiedPackagesClient
    {
        private readonly ExternalBlobStorageClient _storageClient;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public VerifiedPackagesClient(
            ExternalBlobStorageClient storageClient,
            IOptions<NuGetInsightsSettings> options)
        {
            _storageClient = storageClient;
            _options = options;
        }

        public async Task<AsOfData<VerifiedPackage>> GetAsync()
        {
            return await _storageClient.DownloadNewestAsync(
                _options.Value.VerifiedPackagesV1Urls.Select(x => new Uri(x)).ToList(),
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
