using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Microsoft.AspNetCore.SignalR;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Website.Logic
{
    public class PackageReportHub : Hub
    {
        public const string Path = "Hubs/PackageReport";

        private readonly PackageConsistencyService _packageConsistencyService;
        private readonly PackageQueryContextBuilder _packageQueryContextBuilder;
        private readonly LatestCatalogCommitFetcher _latestCatalogCommitFetcher;

        public PackageReportHub(
            PackageConsistencyService packageConsistencyService,
            PackageQueryContextBuilder packageQueryContextBuilder,
            LatestCatalogCommitFetcher latestCatalogCommitFetcher)
        {
            _packageConsistencyService = packageConsistencyService;
            _packageQueryContextBuilder = packageQueryContextBuilder;
            _latestCatalogCommitFetcher = latestCatalogCommitFetcher;
        }
        
        public async Task GetLatest()
        {
            try
            {
                await GetLatestInternalAsync();
            }
            catch
            {
                await InvokeErrorAsync("An internal server error occurred.");
            }
        }

        private async Task GetLatestInternalAsync()
        {
            await InvokeProgressAsync(0, "Fetching the latest catalog commit...");

            var commit = await _latestCatalogCommitFetcher.GetLatestCommitAsync(new ProgressReport(this));
            
            var catalogItem = commit.First();
            await InvokeProgressAsync(1, $"Using package {catalogItem.Id} {catalogItem.Version}.");

            await InvokeFoundLatestAsync(catalogItem.Id, catalogItem.Version.ToFullString());
        }

        public async Task Start(string id, string version)
        {
            try
            {
                await StartInternalAsync(id, version);
            }
            catch
            {
                await InvokeErrorAsync("An internal server error occurred.");
            }
        }

        private async Task StartInternalAsync(string id, string version)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                await InvokeErrorAsync("You must provide a package ID.");
                return;
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                await InvokeErrorAsync("You must provide a package version.");
                return;
            }

            if (!NuGetVersion.TryParse(version, out var parsedVersion))
            {
                await InvokeErrorAsync("The version you provided is invalid.");
                return;
            }

            await InvokeProgressAsync(0, $"Initializing package report for {id} {version}.");

            var state = new PackageConsistencyState();
            var context = await _packageQueryContextBuilder.GetPackageQueryContextFromGalleryAsync(id, version, state);

            var packageDeletedStatus = state.Gallery.PackageState.PackageDeletedStatus;
            var isAvailable = packageDeletedStatus == PackageDeletedStatus.NotDeleted;
            string availability;
            switch (packageDeletedStatus)
            {
                case PackageDeletedStatus.NotDeleted:
                    availability = "is available";
                    break;
                case PackageDeletedStatus.SoftDeleted:
                    availability = "was soft deleted";
                    break;
                case PackageDeletedStatus.Unknown:
                    availability = "was hard deleted or never published";
                    break;
                default:
                    throw new NotImplementedException(
                        $"The package deleted status {packageDeletedStatus} is not supported.");
            }

            await InvokeProgressAsync(
                0,
                $"{context.Package.Id} {context.Package.Version} {availability}" +
                $"{(isAvailable && context.IsSemVer2 ? " and is SemVer 2.0.0" : string.Empty)}. " +
                $"The consistency report will now be generated.");

            var report = await _packageConsistencyService.GetReportAsync(context, state, new ProgressReport(this));

            await InvokeCompleteAsync(report);
        }

        private async Task InvokeProgressAsync(decimal percent, string message)
        {
            await InvokeOnClientAsync("Progress", percent, message);
        }

        private async Task InvokeErrorAsync(string message)
        {
            await InvokeOnClientAsync("Error", message);
        }

        private async Task InvokeCompleteAsync(PackageConsistencyReport report)
        {
            await InvokeOnClientAsync("Complete", report);
        }

        private async Task InvokeFoundLatestAsync(string id, string version)
        {
            await InvokeOnClientAsync("FoundLatest", new { Id = id, Version = version });
        }

        private async Task InvokeOnClientAsync(string method, params object[] args)
        {
            await Clients
                .Client(Context.ConnectionId)
                .InvokeAsync(method, args);
        }

        private class ProgressReport : IProgressReport
        {
            private readonly PackageReportHub _hub;

            public ProgressReport(PackageReportHub hub)
            {
                _hub = hub;
            }

            public async Task ReportProgressAsync(decimal percent, string message)
            {
                await _hub.InvokeProgressAsync(percent, message);
            }
        }
    }
}
