// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.GitHubUsageToCsv
{
    public class GitHubUsageToCsvUpdater : IAuxiliaryFileUpdater<AsOfData<GitHubRepositoryInfo>, GitHubUsageRecord>
    {
        private readonly GitHubUsageClient _client;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public GitHubUsageToCsvUpdater(
            GitHubUsageClient client,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _client = client;
            _options = options;
        }

        public string OperationName => "GitHubUsageToCsv";
        public string Title => "GitHub usage to CSV";
        public string ContainerName => _options.Value.GitHubUsageContainerName;
        public TimeSpan Frequency => _options.Value.GitHubUsageToCsvFrequency;
        public bool HasRequiredConfiguration => _options.Value.GitHubUsageV1Urls is not null && _options.Value.GitHubUsageV1Urls.Count > 0;
        public bool AutoStart => _options.Value.AutoStartGitHubUsageToCsv;

        public async Task<AsOfData<GitHubRepositoryInfo>> GetDataAsync()
        {
            return await _client.GetAsync();
        }

        public async IAsyncEnumerable<IReadOnlyList<GitHubUsageRecord>> ProduceRecordsAsync(IVersionSet versionSet, AsOfData<GitHubRepositoryInfo> data)
        {
            var uniqueDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            const int pageSize = AsOfData<GitHubUsageRecord>.DefaultPageSize;
            var outputPage = new List<GitHubUsageRecord>(pageSize);

            await foreach (IReadOnlyList<GitHubRepositoryInfo> page in data.Pages)
            {
                foreach (GitHubRepositoryInfo repoInfo in page)
                {
                    var repoId = repoInfo.Id;
                    var stars = repoInfo.Stars;

                    foreach (string dependency in repoInfo.Dependencies)
                    {
                        if (!uniqueDependencies.Add(dependency))
                        {
                            continue;
                        }

                        if (!versionSet.TryGetId(dependency, out var packageId))
                        {
                            continue;
                        }

                        outputPage.Add(new GitHubUsageRecord
                        {
                            AsOfTimestamp = data.AsOfTimestamp,
                            ResultType = GitHubUsageResultType.GitHubDependent,
                            LowerId = packageId.ToLowerInvariant(),
                            Id = packageId,
                            Repository = repoId,
                            Stars = stars,
                        });

                        if (outputPage.Count >= pageSize)
                        {
                            yield return outputPage;
                            outputPage.Clear();
                        }
                    }

                    uniqueDependencies.Clear();
                }
            }

            // Add IDs that are not mentioned in the data and therefore are not excluded. This makes joins on the
            // produced data set easier.
            foreach (var packageId in versionSet.GetUncheckedIds())
            {
                outputPage.Add(new GitHubUsageRecord
                {
                    AsOfTimestamp = data.AsOfTimestamp,
                    ResultType = GitHubUsageResultType.NoGitHubDependent,
                    LowerId = packageId.ToLowerInvariant(),
                    Id = packageId,
                    Repository = string.Empty,
                    Stars = null,
                });

                if (outputPage.Count >= pageSize)
                {
                    yield return outputPage;
                    outputPage.Clear();
                }
            }

            if (outputPage.Count > 0)
            {
                yield return outputPage;
            }
        }
    }
}
