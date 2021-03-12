using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Website.Models;
using Knapcode.ExplorePackages.Worker;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knapcode.ExplorePackages.Website.Controllers
{
    [Authorize(Policy = AllowListAuthorizationHandler.PolicyName)]
    public class AdminController : Controller
    {
        private static bool _isInitialized;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly CatalogScanCursorService _catalogScanCursorService;
        private readonly CatalogScanService _catalogScanService;
        private readonly IRemoteCursorClient _remoteCursorClient;
        private readonly IStreamWriterUpdaterService<PackageDownloadSet> _downloadsToCsvService;
        private readonly IStreamWriterUpdaterService<PackageOwnerSet> _ownersToCsvService;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;

        public AdminController(
            IRawMessageEnqueuer rawMessageEnqueuer,
            CatalogScanStorageService catalogScanStorageService,
            CatalogScanCursorService catalogScanCursorService,
            CatalogScanService catalogScanService,
            IRemoteCursorClient remoteCursorClient,
            IStreamWriterUpdaterService<PackageDownloadSet> downloadsToCsvService,
            IStreamWriterUpdaterService<PackageOwnerSet> ownersToCsvService)
        {
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _catalogScanStorageService = catalogScanStorageService;
            _catalogScanCursorService = catalogScanCursorService;
            _catalogScanService = catalogScanService;
            _remoteCursorClient = remoteCursorClient;
            _downloadsToCsvService = downloadsToCsvService;
            _ownersToCsvService = ownersToCsvService;
        }

        public async Task<ViewResult> Index()
        {
            await InitializeAsync();

            var approximateMessageCountTask = _rawMessageEnqueuer.GetApproximateMessageCountAsync();
            var poisonApproximateMessageCountTask = _rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync();
            const int messageCount = 32;
            var availableMessageCountLowerBoundTask = _rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(messageCount);
            var poisonAvailableMessageCountLowerBoundTask = _rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(messageCount);

            var catalogScanTasks = _catalogScanCursorService
                .StartableDriverTypes
                .Select(GetCatalogScanAsync)
                .ToList();

            var isDownloadsToCsvRunningTask = _downloadsToCsvService.IsRunningAsync();
            var isOwnersToCsvRunningTask = _ownersToCsvService.IsRunningAsync();
            var catalogCommitTimestampTask = _remoteCursorClient.GetCatalogAsync();

            await Task.WhenAll(
                approximateMessageCountTask,
                poisonApproximateMessageCountTask,
                availableMessageCountLowerBoundTask,
                poisonAvailableMessageCountLowerBoundTask,
                isDownloadsToCsvRunningTask,
                isOwnersToCsvRunningTask,
                catalogCommitTimestampTask);

            var catalogScans = await Task.WhenAll(catalogScanTasks);

            // Calculate the cursor age.
            var catalogCommitTimestamp = await catalogCommitTimestampTask;
            foreach (var catalogScan in catalogScans)
            {
                var min = catalogScan.Cursor.Value;
                if (min < CatalogClient.NuGetOrgMin)
                {
                    min = CatalogClient.NuGetOrgMin;
                }

                catalogScan.CursorAge = catalogCommitTimestamp - min;
            }

            var model = new AdminViewModel
            {
                ApproximateMessageCount = await approximateMessageCountTask,
                AvailableMessageCountLowerBound = await availableMessageCountLowerBoundTask,
                PoisonApproximateMessageCount = await poisonApproximateMessageCountTask,
                PoisonAvailableMessageCountLowerBound = await poisonAvailableMessageCountLowerBoundTask,
                CatalogScans = catalogScans,
                IsDownloadsToCsvRunning = await isDownloadsToCsvRunningTask,
                IsOwnersToCsvRunning = await isOwnersToCsvRunningTask,
            };

            model.AvailableMessageCountIsExact = model.AvailableMessageCountLowerBound < messageCount;
            model.PoisonAvailableMessageCountIsExact = model.PoisonAvailableMessageCountLowerBound < messageCount;

            return View(model);
        }

        private async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await Task.WhenAll(
                _rawMessageEnqueuer.InitializeAsync(),
                _catalogScanService.InitializeAsync(),
                _downloadsToCsvService.InitializeAsync(),
                _ownersToCsvService.InitializeAsync());

            _isInitialized = true;
        }

        private async Task<CatalogScanViewModel> GetCatalogScanAsync(CatalogScanDriverType driverType)
        {
            var cursor = await _catalogScanCursorService.GetCursorAsync(driverType);
            var latestScans = await _catalogScanStorageService.GetLatestIndexScans(cursor.Name, maxEntities: 5);

            return new CatalogScanViewModel
            {
                DriverType = driverType,
                Cursor = cursor,
                LatestScans = latestScans,
                SupportsReprocess = _catalogScanService.SupportsReprocess(driverType),
                OnlyLatestLeavesSupport = _catalogScanService.GetOnlyLatestLeavesSupport(driverType),
            };
        }

        [HttpPost]
        public async Task<RedirectToActionResult> UpdateAllCatalogScans(
            bool useCustomMax,
            string max)
        {
            (var success, var message, var fragment) = await UpdateAllCatalogScansAsync(useCustomMax, max);
            if (success)
            {
                TempData[fragment + ".Success"] = message;
            }
            else
            {
                TempData[fragment + ".Error"] = message;
            }

            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment);
        }

        private async Task<(bool Success, string Message, string Fragment)> UpdateAllCatalogScansAsync(
            bool useCustomMax,
            string max)
        {
            const string fragment = "CatalogScans";
            var parsedMax = await _remoteCursorClient.GetCatalogAsync();
            if (useCustomMax)
            {
                if (!DateTimeOffset.TryParse(max, out var parsedMaxValue))
                {
                    return (false, "Unable to parse the custom max value.", fragment);
                }

                parsedMax = parsedMaxValue;
            }

            var results = await _catalogScanService.UpdateAllAsync(parsedMax);
            var newStarted = results
                .Where(x => x.Value.Type == CatalogScanServiceResultType.NewStarted)
                .OrderBy(x => x.Key)
                .ToList();

            if (newStarted.Count > 0)
            {
                var firstNewStarted = newStarted[0];
                return (true, GetNewStartedMessage(firstNewStarted.Value), firstNewStarted.Key.ToString());
            }

            return (false, "No catalog scan could be started.", fragment);
        }

        [HttpPost]
        public async Task<RedirectToActionResult> UpdateCatalogScan(
            CatalogScanDriverType driverType,
            bool useCustomMax,
            bool? onlyLatestLeaves,
            bool reprocess,
            string max)
        {
            (var success, var message) = await UpdateCatalogScanAsync(driverType, useCustomMax, onlyLatestLeaves, reprocess, max);
            if (success)
            {
                TempData[driverType + ".Success"] = message;
            }
            else
            {
                TempData[driverType + ".Error"] = message;
            }

            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment: driverType.ToString());
        }

        private async Task<(bool Success, string Message)> UpdateCatalogScanAsync(
            CatalogScanDriverType driverType,
            bool useCustomMax,
            bool? onlyLatestLeaves,
            bool reprocess,
            string max)
        {
            DateTimeOffset? parsedMax = null;
            if (useCustomMax)
            {
                if (reprocess)
                {
                    return (false, "Unable to reprocess with a custom max value.");
                }

                if (!DateTimeOffset.TryParse(max, out var parsedMaxValue))
                {
                    return (false, "Unable to parse the custom max value.");
                }

                parsedMax = parsedMaxValue;
            }

            CatalogScanServiceResult result;
            if (reprocess)
            {
                result = await _catalogScanService.ReprocessAsync(driverType);
            }
            else
            {
                result = await _catalogScanService.UpdateAsync(driverType, parsedMax, onlyLatestLeaves);
            }

            switch (result.Type)
            {
                case CatalogScanServiceResultType.AlreadyRunning:
                    return (false, $"Scan <b>{result.Scan.ScanId}</b> is already running.");
                case CatalogScanServiceResultType.BlockedByDependency:
                    return (false, $"The scan can't use that max because it's beyond the <b>{result.DependencyName}</b> cursor.");
                case CatalogScanServiceResultType.FullyCaughtUpWithDependency:
                    return (true, $"The scan is fully caught up with the <b>{result.DependencyName}</b> cursor.");
                case CatalogScanServiceResultType.MinAfterMax:
                    return (false, $"The provided max is less than the current cursor position.");
                case CatalogScanServiceResultType.NewStarted:
                    return (true, GetNewStartedMessage(result));
                case CatalogScanServiceResultType.UnavailableLease:
                    return (false, $"The lease to start the catalog scan is not available.");
                case CatalogScanServiceResultType.FullyCaughtUpWithMax:
                    return (true, $"The scan is fully caught up with the provided max value.");
                default:
                    throw new NotSupportedException($"The result type {result.Type} is not supported.");
            }
        }

        private static string GetNewStartedMessage(CatalogScanServiceResult result)
        {
            return $"Catalog scan <b>{result.Scan.ScanId}</b> has been started.";
        }

        [HttpPost]
        public async Task<RedirectToActionResult> StartDownloadsToCsv(bool loop)
        {
            await _downloadsToCsvService.StartAsync(loop, notBefore: TimeSpan.Zero);
            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment: "DownloadsToCsv");
        }

        [HttpPost]
        public async Task<RedirectToActionResult> StartOwnersToCsv(bool loop)
        {
            await _ownersToCsvService.StartAsync(loop, notBefore: TimeSpan.Zero);
            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment: "OwnersToCsv");
        }
    }
}
