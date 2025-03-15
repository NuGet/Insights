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

        private async IAsyncEnumerable<IReadOnlyList<VerifiedPackage>> DeserializeAsync(Stream stream)
        {
            var verifiedPackages = await JsonSerializer.DeserializeAsync<List<string>>(stream);
            const int pageSize = AsOfData<VerifiedPackage>.DefaultPageSize;
            var outputPage = new List<VerifiedPackage>(capacity: pageSize);
            foreach (var page in verifiedPackages!.Chunk(pageSize))
            {
                outputPage.AddRange(page.Select(x => new VerifiedPackage(x)));
                yield return outputPage;
                outputPage.Clear();
            }
        }
    }
}
