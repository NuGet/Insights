using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Website.Models;
using Knapcode.ExplorePackages.Worker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knapcode.ExplorePackages.Website.Controllers
{
    [Authorize(Policy = AllowListAuthorizationHandler.PolicyName)]
    public class AdminController : Controller
    {
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly CatalogScanService _catalogScanService;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;

        public AdminController(
            CatalogScanStorageService catalogScanStorageService,
            CatalogScanService catalogScanService,
            IRawMessageEnqueuer rawMessageEnqueuer)
        {
            _catalogScanStorageService = catalogScanStorageService;
            _catalogScanService = catalogScanService;
            _rawMessageEnqueuer = rawMessageEnqueuer;
        }

        public async Task<ViewResult> Index()
        {
            await _catalogScanService.InitializeAsync();
            var approximateMessageCountTask = _rawMessageEnqueuer.GetApproximateMessageCountAsync();
            var poisonApproximateMessageCountTask = _rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync();
            const int messageCount = 32;
            var availableMessageCountLowerBoundTask = _rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(messageCount);
            var poisonAvailableMessageCountLowerBoundTask = _rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(messageCount);

            var catalogScanTasks = Enum
                .GetValues(typeof(CatalogScanType))
                .Cast<CatalogScanType>()
                .Select(GetCatalogScanAsync)
                .ToList();

            await Task.WhenAll(
                approximateMessageCountTask,
                poisonApproximateMessageCountTask,
                availableMessageCountLowerBoundTask,
                poisonAvailableMessageCountLowerBoundTask);

            var catalogScans = await Task.WhenAll(catalogScanTasks);

            var model = new AdminViewModel
            {
                ApproximateMessageCount = await approximateMessageCountTask,
                AvailableMessageCountLowerBound = await availableMessageCountLowerBoundTask,
                PoisonApproximateMessageCount = await poisonApproximateMessageCountTask,
                PoisonAvailableMessageCountLowerBound = await poisonAvailableMessageCountLowerBoundTask,
                CatalogScans = catalogScans,
            };

            model.AvailableMessageCountIsExact = model.AvailableMessageCountLowerBound < messageCount;
            model.PoisonAvailableMessageCountIsExact = model.PoisonAvailableMessageCountLowerBound < messageCount;

            return View(model);
        }

        private async Task<CatalogScanViewModel> GetCatalogScanAsync(CatalogScanType type)
        {
            var cursor = await _catalogScanService.GetCursorAsync(type);
            var latestScans = await _catalogScanStorageService.GetLatestIndexScans(cursor.Name, maxEntities: 10);

            return new CatalogScanViewModel
            {
                Type = type,
                Cursor = cursor,
                LatestScans = latestScans,
            };
        }

        [HttpPost]
        public async Task<RedirectToActionResult> UpdateCatalogScan(CatalogScanType type, bool? shortTest, string max)
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

            await _catalogScanService.UpdateAsync(type, parsedMax);

            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment: type.ToString());
        }
    }
}
