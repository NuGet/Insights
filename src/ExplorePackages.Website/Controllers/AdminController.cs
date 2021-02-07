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
                .Where(x => x != CatalogScanDriverType.FindLatestCatalogLeafScan) // this driver is only used as part of another catalog scan as an implementation detail
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
            var latestScans = await _catalogScanStorageService.GetLatestIndexScans(cursor.Name, maxEntities: 10);

            return new CatalogScanViewModel
            {
                DriverType = driverType,
                Cursor = cursor,
                LatestScans = latestScans,
            };
        }

        [HttpPost]
        public async Task<RedirectToActionResult> UpdateCatalogScan(CatalogScanDriverType driverType, bool? shortTest, bool onlyLatestLeaves, string max)
        {
            DateTimeOffset? parsedMax = null;
            if (shortTest.HasValue)
            {
                parsedMax = shortTest.Value ? DateTimeOffset.Parse("2018-09-20T01:46:19.1755275Z") : (DateTimeOffset?)null;
            }
            else if (!string.IsNullOrWhiteSpace(max))
            {
                parsedMax = DateTimeOffset.Parse(max);
            }

            await _catalogScanService.UpdateAsync(driverType, parsedMax, onlyLatestLeaves);

            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment: driverType.ToString());
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
