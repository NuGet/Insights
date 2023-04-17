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
        private const string CatalogScansFragment = "CatalogScans";

        private readonly ControllerInitializer _initializer;
        private readonly ViewModelFactory _viewModelFactory;
        private readonly CatalogScanService _catalogScanService;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly TimerExecutionService _timerExecutionService;
        private readonly CatalogScanCursorService _catalogScanCursorService;
        private readonly MoveMessagesTaskQueue _moveMessagesTaskQueue;

        public AdminController(
            ControllerInitializer initializer,
            ViewModelFactory service,
            CatalogScanService catalogScanService,
            IRawMessageEnqueuer rawMessageEnqueuer,
            TimerExecutionService timerExecutionService,
            CatalogScanCursorService catalogScanCursorService,
            MoveMessagesTaskQueue moveMessagesTaskQueue)
        {
            _initializer = initializer;
            _viewModelFactory = service;
            _catalogScanService = catalogScanService;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _timerExecutionService = timerExecutionService;
            _catalogScanCursorService = catalogScanCursorService;
            _moveMessagesTaskQueue = moveMessagesTaskQueue;
        }

        [HttpGet]
        public async Task<ViewResult> Index()
        {
            await _initializer.InitializeAsync();

            var model = await _viewModelFactory.GetAdminViewModelAsync();
            return View(model);
        }

        [HttpPost]
        public async Task<RedirectToActionResult> MoveMessages(QueueType source, bool isPoisonSource, QueueType destination, bool isPoisonDestination)
        {
            var task = new MoveMessagesTask(source, isPoisonSource, destination, isPoisonDestination);
            bool success;
            string message;
            if (_moveMessagesTaskQueue.IsScheduled(task))
            {
                success = false;
                message = $"A copy task from {source} {(isPoisonSource ? "poison" : "main")} queue to {destination} {(isPoisonDestination ? "poison" : "main")} queue is already scheduled.";
            }
            else if (_moveMessagesTaskQueue.IsWorking(task))
            {
                success = false;
                message = $"A copy task from {source} {(isPoisonSource ? "poison" : "main")} queue to {destination} {(isPoisonDestination ? "poison" : "main")} queue is already in progress.";
            }
            else
            {
                await _moveMessagesTaskQueue.EnqueueAsync(task);
                success = true;
                message = $"A copy task from {source} {(isPoisonSource ? "poison" : "main")} queue to {destination} {(isPoisonDestination ? "poison" : "main")} queue has been scheduled.";
            }

            return Redirect(success, message, GetFragment(source));
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

            var fragment = GetFragment(queueType);
            TempData[fragment + ".Success"] = $"Cleared the {queueType} {(poison ? "poison" : "main")} queue.";
            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment);
        }

        [HttpPost]
        public async Task<RedirectToActionResult> UpdateAllCatalogScans(
            bool useCustomCursor,
            string cursor,
            bool start,
            bool abort,
            bool overrideCursor)
        {
            DateTimeOffset? parsedCursor = null;
            if (useCustomCursor)
            {
                if (!DateTimeOffset.TryParse(cursor, out var parsedCursorValue))
                {
                    return Redirect(false, "Unable to parse the custom cursor value.", CatalogScansFragment);
                }

                parsedCursor = parsedCursorValue;
            }

            if (start)
            {
                return await UpdateAllCatalogScansAsync(parsedCursor);
            }
            else if (abort)
            {
                var aborted = 0;
                foreach (var driverType in _catalogScanCursorService.StartableDriverTypes)
                {
                    var scan = await _catalogScanService.AbortAsync(driverType);
                    if (scan is not null)
                    {
                        aborted++;
                    }
                }

                if (aborted == 0)
                {
                    return Redirect(false, "No catalog scans were aborted.", CatalogScansFragment);
                }
                else
                {
                    return Redirect(true, $"{aborted} catalog scan(s) have been aborted.", CatalogScansFragment);
                }

            }
            else if (overrideCursor)
            {
                await _catalogScanCursorService.SetAllCursorsAsync(parsedCursor.Value);
                return Redirect(true, $"All catalog scan cursors have been set to <b>{parsedCursor.Value.ToZulu()}</b>.", CatalogScansFragment);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private async Task<RedirectToActionResult> UpdateAllCatalogScansAsync(DateTimeOffset? max)
        {
            var results = await _catalogScanService.UpdateAllAsync(max);
            var newStarted = results
                .Where(x => x.Value.Type == CatalogScanServiceResultType.NewStarted)
                .OrderBy(x => x.Key)
                .ToList();

            if (newStarted.Count > 0)
            {
                var firstNewStarted = newStarted[0];
                return Redirect(true, GetNewStartedMessage(firstNewStarted.Value), firstNewStarted.Key.ToString());
            }

            return Redirect(false, "No catalog scan could be started.", CatalogScansFragment);
        }

        [HttpPost]
        public async Task<RedirectToActionResult> UpdateCatalogScan(
            CatalogScanDriverType driverType,
            bool useCustomCursor,
            bool? onlyLatestLeaves,
            bool reprocess,
            string cursor,
            bool start,
            bool abort,
            bool overrideCursor)
        {
            if (!_catalogScanService.GetOnlyLatestLeavesSupport(driverType).HasValue
                && !onlyLatestLeaves.HasValue)
            {
                onlyLatestLeaves = false;
            }

            var fragment = GetFragment(driverType);

            DateTimeOffset? parsedCursor = null;
            if (useCustomCursor)
            {
                if (reprocess)
                {
                    return Redirect(false, "Unable to reprocess with a custom max value.", fragment);
                }

                if (!DateTimeOffset.TryParse(cursor, out var parsedCursorValue))
                {
                    return Redirect(false, "Unable to parse the custom max value.", fragment);
                }

                parsedCursor = parsedCursorValue;
            }

            if (start)
            {
                (var success, var message) = await UpdateCatalogScanAsync(driverType, onlyLatestLeaves, reprocess, parsedCursor);
                return Redirect(success, message, fragment);
            }
            else if (abort)
            {
                var aborted = await _catalogScanService.AbortAsync(driverType);
                if (aborted is not null)
                {
                    return Redirect(true, $"Catalog scan <b>{aborted.GetScanId()}</b> has been aborted.", fragment);
                }
                else
                {
                    return Redirect(false, "There was no incomplete catalog scan to abort.", fragment);
                }
            }
            else if (overrideCursor)
            {
                await _catalogScanCursorService.SetCursorAsync(driverType, parsedCursor.Value);
                return Redirect(true, $"The cursor has been set to <b>{parsedCursor.Value.ToZulu()}</b>.", fragment);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private async Task<(bool Success, string Message)> UpdateCatalogScanAsync(
            CatalogScanDriverType driverType,
            bool? onlyLatestLeaves,
            bool reprocess,
            DateTimeOffset? max)
        {
            CatalogScanServiceResult result;
            if (reprocess)
            {
                result = await _catalogScanService.ReprocessAsync(driverType);
            }
            else
            {
                result = await _catalogScanService.UpdateAsync(driverType, max, onlyLatestLeaves);
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

        private static string GetNewStartedMessage(CatalogScanServiceResult result)
        {
            return $"Catalog scan <b>{result.Scan.GetScanId()}</b> has been started.";
        }

        private static string GetFragment(QueueType source)
        {
            return source.ToString() + "Queue";
        }

        private static string GetFragment(CatalogScanDriverType driverType)
        {
            return driverType.ToString();
        }

        private RedirectToActionResult Redirect(bool success, string message, string fragment)
        {
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
    }
}
