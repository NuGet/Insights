// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NuGet.Insights.Worker;
using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Website.Controllers
{
    [Authorize(Policy = AllowListAuthorizationHandler.PolicyName)]
    public class AdminController : Controller
    {
        private const string CatalogScansFragment = "CatalogScans";

        private readonly ViewModelFactory _viewModelFactory;
        private readonly CatalogScanService _catalogScanService;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly TimerExecutionService _timerExecutionService;
        private readonly CatalogScanCursorService _catalogScanCursorService;
        private readonly MoveMessagesTaskQueue _moveMessagesTaskQueue;

        public AdminController(
            ViewModelFactory service,
            CatalogScanService catalogScanService,
            IRawMessageEnqueuer rawMessageEnqueuer,
            TimerExecutionService timerExecutionService,
            CatalogScanCursorService catalogScanCursorService,
            MoveMessagesTaskQueue moveMessagesTaskQueue)
        {
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
            bool overrideCursor,
            bool reset)
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
                var scans = await _catalogScanService.AbortAllAsync();
                if (scans.Count == 0)
                {
                    return Redirect(false, "No catalog scans were aborted.", CatalogScansFragment);
                }
                else
                {
                    return Redirect(true, $"{scans.Count} catalog scan(s) have been aborted.", CatalogScansFragment);
                }
            }
            else if (overrideCursor)
            {
                await _catalogScanCursorService.SetAllCursorsAsync(parsedCursor.Value);
                return Redirect(true, $"All catalog scan cursors have been set to <b>{parsedCursor.Value.ToZulu()}</b>.", CatalogScansFragment);
            }
            else if (reset)
            {
                await _catalogScanCursorService.SetAllCursorsAsync(CursorTableEntity.Min);
                await _catalogScanService.DestroyAllOutputAsync();
                return Redirect(true, $"All driver cursors have been reset and their output has been destroyed.", CatalogScansFragment);
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
            string cursor,
            bool useBucketRanges,
            string bucketRanges,
            bool start,
            bool abort,
            bool overrideCursor,
            bool reset)
        {
            if (!CatalogScanDriverMetadata.GetOnlyLatestLeavesSupport(driverType).HasValue
                && !onlyLatestLeaves.HasValue)
            {
                onlyLatestLeaves = false;
            }

            var fragment = GetFragment(driverType);

            DateTimeOffset? parsedCursor = null;
            if (useCustomCursor)
            {
                if (!DateTimeOffset.TryParse(cursor, out var parsedCursorValue))
                {
                    return Redirect(false, "Unable to parse the custom max value.", fragment);
                }

                parsedCursor = parsedCursorValue;
            }

            if (start)
            {
                CatalogScanServiceResult result;
                if (useBucketRanges)
                {
                    List<int> parsedBuckets;
                    try
                    {
                        parsedBuckets = BucketRange.ParseBuckets(bucketRanges).ToList();
                    }
                    catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentOutOfRangeException)
                    {
                        return Redirect(success: false, message: $"Could not parse bucket ranges: {bucketRanges}.<br />" + ex.Message, fragment);
                    }

                    if (parsedBuckets.Count == 0)
                    {
                        return Redirect(success: false, message: $"At least one bucket must be specified.", fragment);
                    }

                    result = await _catalogScanService.UpdateAsync(driverType, parsedBuckets);
                }
                else
                {
                    result = await _catalogScanService.UpdateAsync(driverType, parsedCursor, onlyLatestLeaves);
                }

                (var success, var message) = HandleCatalogScanServiceResult(result, useBucketRanges);
                return Redirect(success, message, fragment);
            }
            else if (abort)
            {
                var aborted = await _catalogScanService.AbortAsync(driverType);
                if (aborted is not null)
                {
                    return Redirect(true, $"Catalog scan <b>{aborted.ScanId}</b> has been aborted.", fragment);
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
            else if (reset)
            {
                await _catalogScanCursorService.SetCursorAsync(driverType, CursorTableEntity.Min);
                await _catalogScanService.DestroyOutputAsync(driverType);
                return Redirect(true, $"The driver's cursor has been reset and its output has been destroyed.", fragment);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private (bool Success, string Message) HandleCatalogScanServiceResult(CatalogScanServiceResult result, bool useBucketRanges)
        {
            switch (result.Type)
            {
                case CatalogScanServiceResultType.AlreadyStarted:
                    return (false, $"Scan <b>{result.Scan.ScanId}</b> was already started.");
                case CatalogScanServiceResultType.BlockedByDependency when !useBucketRanges:
                    return (false, $"The scan can't use that max because it's beyond the <b>{result.DependencyName}</b> cursor.");
                case CatalogScanServiceResultType.BlockedByDependency when useBucketRanges:
                    return (false, $"The scan can't start because the <b>{result.DependencyName}</b> cursor doesn't match this driver's cursor.");
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
        public async Task<RedirectToActionResult> UpdateTimer(
            string timerName,
            bool runNow,
            bool disable,
            bool enable,
            bool abort,
            bool reset,
            bool setNextRun,
            string nextRun)
        {
            if (runNow)
            {
                var executed = await _timerExecutionService.ExecuteNowAsync(timerName);
                if (!executed)
                {
                    TempData[timerName + ".Error"] = "The timer could not be executed.";
                }
            }
            else if (disable)
            {
                await _timerExecutionService.SetIsEnabledAsync(timerName, isEnabled: false);
            }
            else if (enable)
            {
                await _timerExecutionService.SetIsEnabledAsync(timerName, isEnabled: true);
            }
            else if (abort)
            {
                await _timerExecutionService.SetIsEnabledAsync(timerName, isEnabled: false);
                await _timerExecutionService.AbortAsync(timerName);
                TempData[timerName + ".Success"] = "The timer has been disabled and aborted.";
            }
            else if (reset)
            {
                await _timerExecutionService.DestroyOutputAsync(timerName);
                TempData[timerName + ".Success"] = "The output of the timer has been destroyed.";
            }
            else if (setNextRun)
            {
                if (!DateTimeOffset.TryParse(nextRun, out var parsedNextRun))
                {
                    TempData[timerName + ".Error"] = $"The next run timestamp could not be parsed.";
                }
                else
                {
                    await _timerExecutionService.SetNextRunAsync(timerName, parsedNextRun);
                    TempData[timerName + ".Success"] = $"The timer's next run time has been updated to {parsedNextRun.ToZulu()}.";
                }
            }

            return RedirectToAction(nameof(Index), ControllerContext.ActionDescriptor.ControllerName, fragment: timerName);
        }

        private static string GetNewStartedMessage(CatalogScanServiceResult result)
        {
            return $"Catalog scan <b>{result.Scan.ScanId}</b> has been started.";
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
