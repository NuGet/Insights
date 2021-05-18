// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class PackagesContainerConsistencyService : IConsistencyService<PackagesContainerConsistencyReport>
    {
        private readonly PackagesContainerClient _client;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public PackagesContainerConsistencyService(
            PackagesContainerClient client,
            IOptions<NuGetInsightsSettings> options)
        {
            _client = client;
            _options = options;
        }

        public async Task<PackagesContainerConsistencyReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var shouldExist = !context.IsDeleted;

            await PopulateStateAsync(context, state, progressReporter);

            var isConsistent = shouldExist == state.PackagesContainer.PackageContentMetadata.Exists;

            return new PackagesContainerConsistencyReport(
                isConsistent,
                state.PackagesContainer.PackageContentMetadata);
        }

        public async Task<bool> IsConsistentAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter);
            return report.IsConsistent;
        }

        public async Task PopulateStateAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            if (state.PackagesContainer.PackageContentMetadata != null)
            {
                return;
            }

            var packageContentMetadata = await _client.GetPackageContentMetadataAsync(
                   _options.Value.PackagesContainerBaseUrl,
                   context.Id,
                   context.Version);

            state.PackagesContainer.PackageContentMetadata = packageContentMetadata;
        }
    }
}
