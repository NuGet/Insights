// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.GitHubUsageToCsv
{
    public partial record GitHubUsageRecord : IAuxiliaryFileCsvRecord<GitHubUsageRecord>
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        [BucketKey]
        [KustoPartitionKey]
        public string LowerId { get; set; }

        public string Id { get; set; }

        [Required]
        public GitHubUsageResultType ResultType { get; set; }

        /// <remarks>
        /// Will be an empty string when <see cref="ResultType"/> is <see cref="GitHubUsageResultType.NoGitHubDependent"/>.
        /// </remarks>
        public string Repository { get; set; }

        public int? Stars { get; set; }

        public static IEqualityComparer<GitHubUsageRecord> KeyComparer => GitHubUsageRecordKeyComparer.Instance;
        public static IReadOnlyList<string> KeyFields { get; } = [nameof(LowerId), nameof(Repository)];

        public int CompareTo(GitHubUsageRecord other)
        {
            var c = string.CompareOrdinal(LowerId, other.LowerId);
            if (c != 0)
            {
                return c;
            }

            return string.Compare(Repository, other.Repository, StringComparison.OrdinalIgnoreCase);
        }

        public static List<GitHubUsageRecord> Prune(
            List<GitHubUsageRecord> records,
            bool isFinalPrune,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger logger)
        {
            // no duplicates are expected
            return records.Order().ToList();
        }

        public class GitHubUsageRecordKeyComparer : IEqualityComparer<GitHubUsageRecord>
        {
            public static GitHubUsageRecordKeyComparer Instance { get; } = new GitHubUsageRecordKeyComparer();

            public bool Equals(GitHubUsageRecord x, GitHubUsageRecord y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.LowerId == y.LowerId
                    && string.Equals(x.Repository, y.Repository, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode([DisallowNull] GitHubUsageRecord obj)
            {
                var hashCode = new HashCode();
                hashCode.Add(obj.LowerId);
                hashCode.Add(obj.Repository, StringComparer.OrdinalIgnoreCase);
                return hashCode.ToHashCode();
            }
        }
    }
}
