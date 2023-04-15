// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NuGet.Insights.Worker;

namespace NuGet.Insights.Website.Controllers
{
    [Authorize(Policy = AllowListAuthorizationHandler.PolicyName)]
    public class AdminController : Controller
    {
        private readonly ControllerInitializer _initializer;
        private readonly ViewModelFactory _viewModelFactory;
        private readonly CatalogScanService _catalogScanService;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly TimerExecutionService _timerExecutionService;

        public AdminController(
            ControllerInitializer initializer,
            ViewModelFactory service,
            CatalogScanService catalogScanService,
            IRawMessageEnqueuer rawMessageEnqueuer,
            TimerExecutionService timerExecutionService)
        {
            _initializer = initializer;
            _viewModelFactory = service;
            _catalogScanService = catalogScanService;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _timerExecutionService = timerExecutionService;
        }

        [HttpGet]
        public async Task<ViewResult> Index()
        {
            await _initializer.InitializeAsync();

            var model = await _viewModelFactory.GetAdminViewModelAsync();
            return View(model);
        }

        [HttpPost]
        public async Task<RedirectToActionResult> ClearQueue(QueueType queueType, bool poison)
        {
            if (poison)
            {
                await _rawMessageEnqueuer.ClearPoisonAsync(queueType);
            }
            else
            {
                await _rawMessageEnqueuer.ClearAsync(queueType);
            }

            var fragment = queueType.ToString() + "Queue";
            TempData[fragment + ".Success"] = $"Cleared the {queueType} {(poison ? "main" : "poison")} queue.";
            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment);
        }

        [HttpPost]
        public async Task<RedirectToActionResult> UpdateAllCatalogScans(
            bool useCustomMax,
            string max,
            bool start,
            bool overrideCursor)
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

        private async Task<(bool Success, string Message, string Fragment)> UpdateAllCatalogScansAsync(bool useCustomMax, string max)
        {
            const string fragment = "CatalogScans";

            DateTimeOffset? parsedMax = null;
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
            if (!_catalogScanService.GetOnlyLatestLeavesSupport(driverType).HasValue
                && !onlyLatestLeaves.HasValue)
            {
                onlyLatestLeaves = false;
            }

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
                    return (false, $"Scan <b>{result.Scan.GetScanId()}</b> is already running.");
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
                case CatalogScanServiceResultType.Disabled:
                    return (false, $"This driver is disabled in configuration.");
                default:
                    throw new NotSupportedException($"The result type {result.Type} is not supported.");
            }
        }

        private static string GetNewStartedMessage(CatalogScanServiceResult result)
        {
            return $"Catalog scan <b>{result.Scan.GetScanId()}</b> has been started.";
        }

        [HttpPost]
        public async Task<RedirectToActionResult> UpdateTimer(string timerName, bool? runNow, bool? disable, bool? enable)
        {
            if (runNow == true)
            {
                var executed = await _timerExecutionService.ExecuteNowAsync(timerName);
                if (!executed)
                {
                    TempData[timerName + ".Error"] = "The timer could not be executed.";
                }
            }
            else if (disable == true)
            {
                await _timerExecutionService.SetIsEnabledAsync(timerName, isEnabled: false);
            }
            else if (enable == true)
            {
                await _timerExecutionService.SetIsEnabledAsync(timerName, isEnabled: true);
            }

            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment: timerName);
        }
    }
}
