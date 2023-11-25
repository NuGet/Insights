// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class GalleryConsistencyService : IConsistencyService<GalleryConsistencyReport>
    {
        private readonly GalleryClient _client;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public GalleryConsistencyService(
            GalleryClient client,
            IOptions<NuGetInsightsSettings> settings)
        {
            _client = client;
            _options = settings;
        }

        public async Task<GalleryConsistencyReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var shouldExist = !context.IsDeleted;

            await PopulateStateAsync(context, state, progressReporter);
            var actuallyExists = state.Gallery.PackageState.PackageDeletedStatus == PackageDeletedStatus.NotDeleted;

            var isConsistent = shouldExist == actuallyExists;
            if (shouldExist)
            {
                isConsistent &= state.Gallery.PackageState.HasIcon == context.HasIcon;
                isConsistent &= state.Gallery.PackageState.IsListed == context.IsListed;
            }

            return new GalleryConsistencyReport(
                isConsistent,
                state.Gallery.PackageState);
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
            if (state.Gallery.PackageState != null)
            {
                return;
            }

            var packageState = await _client.GetPackageStateAsync(
                _options.Value.GalleryBaseUrl,
                context.Id,
                context.Version);

            state.Gallery.PackageState = packageState;
        }
    }
}
