using System;
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
        
        public PackageReportHub(
            PackageConsistencyService packageConsistencyService,
            PackageQueryContextBuilder packageQueryContextBuilder)
        {
            _packageConsistencyService = packageConsistencyService;
            _packageQueryContextBuilder = packageQueryContextBuilder;
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

            await InvokeStatusAsync($"Initializing package report for {id} {version}.");

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

            await InvokeStatusAsync(
                $"{context.Package.Id} {context.Package.Version} {availability}" +
                $"{(isAvailable && context.IsSemVer2 ? " and is SemVer 2.0.0" : string.Empty)}. " +
                $"The consistency report will now be generated.");

            await InvokeCompleteAsync();
        }

        private async Task InvokeStatusAsync(string message)
        {
            await InvokeOnClientAsync("Status", message);
        }

        private async Task InvokeErrorAsync(string message)
        {
            await InvokeOnClientAsync("Error", message);
            Context.Connection.Abort();
        }

        private async Task InvokeCompleteAsync()
        {
            await InvokeOnClientAsync("Complete");
            Context.Connection.Abort();
        }

        private async Task InvokeOnClientAsync(string method, params object[] args)
        {
            await Clients
                .Client(Context.ConnectionId)
                .InvokeAsync(method, args);
        }
    }
}
