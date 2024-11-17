// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class ExcludedPackagesClient
    {
        private readonly ExternalBlobStorageClient _storageClient;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public ExcludedPackagesClient(
            ExternalBlobStorageClient storageClient,
            IOptions<NuGetInsightsSettings> options)
        {
            _storageClient = storageClient;
            _options = options;
        }

        public async Task<AsOfData<ExcludedPackage>> GetAsync()
        {
            return await _storageClient.DownloadNewestAsync(
                _options.Value.ExcludedPackagesV1Urls.Select(x => new Uri(x)).ToList(),
                _options.Value.ExcludedPackagesV1AgeLimit,
                "ExcludedPackages.v1.json",
                DeserializeAsync);
        }

        private async IAsyncEnumerable<IReadOnlyList<ExcludedPackage>> DeserializeAsync(Stream stream)
        {
            var verifiedPackages = await JsonSerializer.DeserializeAsync<List<string>>(stream);
            const int pageSize = AsOfData<ExcludedPackage>.DefaultPageSize;
            var outputPage = new List<ExcludedPackage>(capacity: pageSize);
            foreach (var page in verifiedPackages!.Chunk(pageSize))
            {
                outputPage.AddRange(page.Select(x => new ExcludedPackage(x)));
                yield return outputPage;
                outputPage.Clear();
            }
        }
    }
}
