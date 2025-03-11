// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class GitHubUsageClient
    {
        private readonly ExternalBlobStorageClient _storageClient;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public GitHubUsageClient(
            ExternalBlobStorageClient storageClient,
            IOptions<NuGetInsightsSettings> options)
        {
            _storageClient = storageClient;
            _options = options;
        }

        public async Task<AsOfData<GitHubRepositoryInfo>> GetAsync()
        {
            return await _storageClient.DownloadNewestAsync(
                _options.Value.GitHubUsageV1Urls.Select(x => new Uri(x)).ToList(),
                _options.Value.GitHubUsageV1AgeLimit,
                "GitHubUsage.v1.json",
                DeserializeAsync);
        }

        private async IAsyncEnumerable<IReadOnlyList<GitHubRepositoryInfo>> DeserializeAsync(Stream stream)
        {
            var repositories = JsonSerializer.DeserializeAsyncEnumerable<GitHubRepositoryInfo>(stream);
            const int pageSize = AsOfData<GitHubRepositoryInfo>.DefaultPageSize;
            var page = new List<GitHubRepositoryInfo>(capacity: pageSize);
            await foreach (var repo in repositories)
            {
                if (repo is null)
                {
                    throw new InvalidDataException("Expected a non-null array element.");
                }

                page.Add(repo);

                if (page.Count >= pageSize)
                {
                    yield return page;
                    page.Clear();
                }
            }

            if (page.Count > 0)
            {
                yield return page;
            }
        }
    }
}
