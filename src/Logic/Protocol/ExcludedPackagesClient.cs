// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class ExcludedPackagesClient
    {
        private readonly BlobStorageJsonClient _storageClient;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public ExcludedPackagesClient(
            BlobStorageJsonClient storageClient,
            IOptions<NuGetInsightsSettings> options)
        {
            _storageClient = storageClient;
            _options = options;
        }

        public async Task<AsOfData<ExcludedPackage>> GetAsync()
        {
            return await _storageClient.DownloadNewestAsync(
                _options.Value.ExcludedPackagesV1Urls,
                _options.Value.ExcludedPackagesV1AgeLimit,
                "ExcludedPackages.v1.json",
                DeserializeAsync);
        }

        private IAsyncEnumerable<ExcludedPackage> DeserializeAsync(Stream stream)
        {
            return JsonSerializer
                .DeserializeAsyncEnumerable<string>(stream)
                .Select(x => new ExcludedPackage(x));
        }
    }
}
