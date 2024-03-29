﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageConsistencyService : IConsistencyService<PackageConsistencyReport>
    {
        private readonly GalleryConsistencyService _gallery;
        private readonly V2ConsistencyService _v2;
        private readonly PackagesContainerConsistencyService _packagesContainer;
        private readonly FlatContainerConsistencyService _flatContainer;
        private readonly RegistrationOriginalConsistencyService _registrationOriginal;
        private readonly RegistrationGzippedConsistencyService _registrationGzipped;
        private readonly RegistrationSemVer2ConsistencyService _registrationSemVer2;
        private readonly SearchConsistencyService _search;
        private readonly CrossCheckConsistencyService _crossCheck;

        public PackageConsistencyService(
            GalleryConsistencyService gallery,
            V2ConsistencyService v2,
            PackagesContainerConsistencyService packagesContainer,
            FlatContainerConsistencyService flatContainer,
            RegistrationOriginalConsistencyService registrationOriginal,
            RegistrationGzippedConsistencyService registrationGzipped,
            RegistrationSemVer2ConsistencyService registrationSemVer2,
            SearchConsistencyService search,
            CrossCheckConsistencyService crossCheck)
        {
            _gallery = gallery;
            _v2 = v2;
            _packagesContainer = packagesContainer;
            _flatContainer = flatContainer;
            _registrationOriginal = registrationOriginal;
            _registrationGzipped = registrationGzipped;
            _registrationSemVer2 = registrationSemVer2;
            _search = search;
            _crossCheck = crossCheck;
        }

        private static async Task AddAsync<TReport>(
            MutableReport report,
            IConsistencyService<TReport> service,
            Action<MutableReport, TReport> addPartialReport,
            string message) where TReport : IConsistencyReport
        {
            var min = (report.Processed + 0) / (decimal)report.Total;
            var max = (report.Processed + 1) / (decimal)report.Total;

            var childReport = await service.GetReportAsync(
                report.Context,
                report.State,
                new PartialProgressReporter(report.ProgressReporter, min, max));

            report.Processed++;
            addPartialReport(report, childReport);
            report.IsConsistent &= childReport.IsConsistent;

            await report.ProgressReporter.ReportProgressAsync(max, message);
        }

        public async Task<PackageConsistencyReport> GetReportAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            var report = new MutableReport
            {
                Context = context,
                State = state,
                ProgressReporter = progressReporter,
                Processed = 0,
                IsConsistent = true,
            };

            await AddAsync(report, _gallery, (r, s) => r.Gallery = s, "Fetched the gallery report.");
            await AddAsync(report, _v2, (r, s) => r.V2 = s, "Fetched the V2 report.");
            await AddAsync(report, _packagesContainer, (r, s) => r.PackagesContainer = s, "Fetched the packages container report.");
            await AddAsync(report, _flatContainer, (r, s) => r.FlatContainer = s, "Fetched the flat container report.");
            await AddAsync(report, _registrationOriginal, (r, s) => r.RegistrationOriginal = s, "Fetched the original registration report.");
            await AddAsync(report, _registrationGzipped, (r, s) => r.RegistrationGzipped = s, "Fetched the gzipped registration report.");
            await AddAsync(report, _registrationSemVer2, (r, s) => r.RegistrationSemVer2 = s, "Fetched the SemVer 2.0.0 registration report.");
            await AddAsync(report, _search, (r, s) => r.Search = s, "Fetched the search registration report.");
            await AddAsync(report, _crossCheck, (r, s) => r.CrossCheck = s, "Fetched the cross check report.");

            return new PackageConsistencyReport(
                context,
                report.IsConsistent,
                report.Gallery,
                report.V2,
                report.PackagesContainer,
                report.FlatContainer,
                report.RegistrationOriginal,
                report.RegistrationGzipped,
                report.RegistrationSemVer2,
                report.Search,
                report.CrossCheck);
        }

        public async Task<bool> IsConsistentAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            if (!(await _gallery.IsConsistentAsync(context, state, progressReporter)))
            {
                return false;
            }

            if (!(await _v2.IsConsistentAsync(context, state, progressReporter)))
            {
                return false;
            }

            if (!(await _packagesContainer.IsConsistentAsync(context, state, progressReporter)))
            {
                return false;
            }

            if (!(await _flatContainer.IsConsistentAsync(context, state, progressReporter)))
            {
                return false;
            }

            if (!(await _registrationOriginal.IsConsistentAsync(context, state, progressReporter)))
            {
                return false;
            }

            if (!(await _registrationGzipped.IsConsistentAsync(context, state, progressReporter)))
            {
                return false;
            }

            if (!(await _registrationOriginal.IsConsistentAsync(context, state, progressReporter)))
            {
                return false;
            }

            if (!(await _search.IsConsistentAsync(context, state, progressReporter)))
            {
                return false;
            }

            if (!(await _crossCheck.IsConsistentAsync(context, state, progressReporter)))
            {
                return false;
            }

            return true;
        }

        public async Task PopulateStateAsync(
            PackageConsistencyContext context,
            PackageConsistencyState state,
            IProgressReporter progressReporter)
        {
            await _gallery.PopulateStateAsync(context, state, progressReporter);
            await _v2.PopulateStateAsync(context, state, progressReporter);
            await _packagesContainer.PopulateStateAsync(context, state, progressReporter);
            await _flatContainer.PopulateStateAsync(context, state, progressReporter);
            await _registrationOriginal.PopulateStateAsync(context, state, progressReporter);
            await _registrationGzipped.PopulateStateAsync(context, state, progressReporter);
            await _registrationSemVer2.PopulateStateAsync(context, state, progressReporter);
            await _search.PopulateStateAsync(context, state, progressReporter);
            await _crossCheck.PopulateStateAsync(context, state, progressReporter);
        }

        private class MutableReport
        {
            public PackageConsistencyContext Context { get; set; }
            public PackageConsistencyState State { get; set; }
            public IProgressReporter ProgressReporter { get; set; }
            public int Processed { get; set; }
            public int Total => 9;

            public bool IsConsistent { get; set; }
            public GalleryConsistencyReport Gallery { get; set; }
            public V2ConsistencyReport V2 { get; set; }
            public PackagesContainerConsistencyReport PackagesContainer { get; set; }
            public FlatContainerConsistencyReport FlatContainer { get; set; }
            public RegistrationConsistencyReport RegistrationOriginal { get; set; }
            public RegistrationConsistencyReport RegistrationGzipped { get; set; }
            public RegistrationConsistencyReport RegistrationSemVer2 { get; set; }
            public SearchConsistencyReport Search { get; set; }
            public CrossCheckConsistencyReport CrossCheck { get; set; }
        }
    }
}
