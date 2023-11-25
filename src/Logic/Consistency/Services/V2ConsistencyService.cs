// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class V2ConsistencyService : IConsistencyService<V2ConsistencyReport>
    {
        private readonly V2Client _client;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public V2ConsistencyService(
            V2Client client,
            IOptions<NuGetInsightsSettings> options)
        {
            _client = client;
            _options = options;
        }

        public async Task<V2ConsistencyReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var incrementalProgress = new IncrementalProgress(progressReporter, 1);
            var shouldExist = !context.IsDeleted;

            var report = new MutableReport { IsConsistent = true };

            var package = await _client.GetPackageOrNullAsync(
                _options.Value.V2BaseUrl,
                context.Id,
                context.Version);
            report.HasPackage = package != null;
            report.IsConsistent &= shouldExist == report.HasPackage;
            await incrementalProgress.ReportProgressAsync("Checked for the package in V2.");

            return new V2ConsistencyReport(
                report.IsConsistent,
                report.HasPackage.Value);
        }

        public async Task<bool> IsConsistentAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter);
            return report.IsConsistent;
        }

        public Task PopulateStateAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            return Task.CompletedTask;
        }

        private class MutableReport
        {
            public bool IsConsistent { get; set; }
            public bool? HasPackage { get; set; }
        }
    }
}
