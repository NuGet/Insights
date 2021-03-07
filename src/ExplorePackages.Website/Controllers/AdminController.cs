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
        private readonly CatalogScanService _catalogScanService;
        private readonly IStreamWriterUpdaterService<PackageDownloadSet> _downloadsToCsvService;
        private readonly IStreamWriterUpdaterService<PackageOwnerSet> _ownersToCsvService;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;

        public AdminController(
            IRawMessageEnqueuer rawMessageEnqueuer,
            CatalogScanStorageService catalogScanStorageService,
            CatalogScanService catalogScanService,
            IStreamWriterUpdaterService<PackageDownloadSet> downloadsToCsvService,
            IStreamWriterUpdaterService<PackageOwnerSet> ownersToCsvService)
        {
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _catalogScanStorageService = catalogScanStorageService;
            _catalogScanService = catalogScanService;
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

            var catalogScanTasks = Enum
                .GetValues(typeof(CatalogScanDriverType))
                .Cast<CatalogScanDriverType>()
                .Where(x => x != CatalogScanDriverType.Internal_FindLatestCatalogLeafScan
                         && x != CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId)
                .Select(GetCatalogScanAsync)
                .ToList();

            var isDownloadsToCsvRunningTask = _downloadsToCsvService.IsRunningAsync();
            var isOwnersToCsvRunningTask = _ownersToCsvService.IsRunningAsync();

            await Task.WhenAll(
                approximateMessageCountTask,
                poisonApproximateMessageCountTask,
                availableMessageCountLowerBoundTask,
                poisonAvailableMessageCountLowerBoundTask,
                isDownloadsToCsvRunningTask,
                isOwnersToCsvRunningTask);

            var catalogScans = await Task.WhenAll(catalogScanTasks);

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
            var cursor = await _catalogScanService.GetCursorAsync(driverType);
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
        public async Task<RedirectToActionResult> UpdateCatalogScan(
            CatalogScanDriverType driverType,
            bool useCustomMax,
            bool? onlyLatestLeaves,
            bool reprocess,
            string max)
        {
            (var success, var message) = await GetUpdateCatalogScanErrorAsync(driverType, useCustomMax, onlyLatestLeaves, reprocess, max);
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

        private async Task<(bool Success, string Message)> GetUpdateCatalogScanErrorAsync(
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
                    return (true, $"Catalog scan <b>{result.Scan.ScanId}</b> has been started.");
                case CatalogScanServiceResultType.UnavailableLease:
                    return (false, $"The lease to start the catalog scan is not available.");
                case CatalogScanServiceResultType.FullyCaughtUpWithMax:
                    return (true, $"The scan is fully caught up with the provided max value.");
                default:
                    throw new NotSupportedException($"The result type {result.Type} is not supported.");
            }
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
