﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class RegistrationConsistencyService : IConsistencyService<RegistrationConsistencyReport>
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly RegistrationClient _client;
        private readonly string _type;
        private readonly bool _hasSemVer2;

        public RegistrationConsistencyService(
            ServiceIndexCache serviceIndexCache,
            RegistrationClient client,
            string type,
            bool hasSemVer2)
        {
            _serviceIndexCache = serviceIndexCache;
            _client = client;
            _type = type;
            _hasSemVer2 = hasSemVer2;
        }

        public async Task<RegistrationConsistencyReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter, allowPartial: false);
            return new RegistrationConsistencyReport(
                report.IsConsistent,
                report.IsInIndex.Value,
                report.HasLeaf.Value,
                report.IsListedInIndex.Value,
                report.IsListedInLeaf.Value);
        }

        public async Task<bool> IsConsistentAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = await GetReportAsync(context, state, progressReporter, allowPartial: true);
            return report.IsConsistent;
        }

        public Task PopulateStateAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            return Task.CompletedTask;
        }

        private async Task<MutableReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter,
            bool allowPartial)
        {
            var report = new MutableReport { IsConsistent = true };
            var incrementalProgress = new IncrementalProgress(progressReporter, 2);
            var baseUrl = await _serviceIndexCache.GetUrlAsync(_type);

            var shouldExist = !context.IsDeleted && (_hasSemVer2 || !context.IsSemVer2);
            var shouldBeListed = shouldExist && context.IsListed;

            var registrationLeafItem = await _client.GetRegistrationLeafItemOrNullAsync(
                baseUrl,
                context.Id,
                context.Version);
            report.IsInIndex = registrationLeafItem != null;
            report.IsListedInIndex = registrationLeafItem?.CatalogEntry.Listed ?? false;
            report.IsConsistent &= shouldExist == report.IsInIndex && shouldBeListed == report.IsListedInIndex;
            await incrementalProgress.ReportProgressAsync("Checked for the package in the registration index.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            var registrationLeaf = await _client.GetRegistrationLeafOrNullAsync(
                baseUrl,
                context.Id,
                context.Version);
            report.HasLeaf = registrationLeaf != null;
            report.IsListedInLeaf = registrationLeaf?.Listed ?? false;
            report.IsConsistent &= shouldExist == report.HasLeaf && shouldBeListed == report.IsListedInLeaf;
            await incrementalProgress.ReportProgressAsync("Checked for the package's registration leaf.");

            if (allowPartial && !report.IsConsistent)
            {
                return report;
            }

            return report;
        }

        private class MutableReport
        {
            public bool IsConsistent { get; set; }
            public bool? IsInIndex { get; set; }
            public bool? HasLeaf { get; set; }
            public bool? IsListedInIndex { get; set; }
            public bool? IsListedInLeaf { get; set; }
        }
    }
}
