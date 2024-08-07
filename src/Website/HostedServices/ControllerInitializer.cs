// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Website
{
    public class ControllerInitializer : IAsyncDisposable
    {
        private Task _currentTask;
        private Task _loopTask;
        private readonly CancellationTokenSource _cts;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ControllerInitializer> _logger;

        public ControllerInitializer(
            IServiceProvider serviceProvider,
            ILogger<ControllerInitializer> logger)
        {
            _cts = new CancellationTokenSource();
            _serviceProvider = serviceProvider;
            _logger = logger;
            _currentTask = InitializeInternalAsync();
            _loopTask = InitializeLoopAsync();
        }

        private async Task InitializeLoopAsync()
        {
            do
            {
                try
                {
                    await _currentTask;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The controller initializer failed.");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    _currentTask = InitializeInternalAsync();
                }
            }
            while (!_cts.IsCancellationRequested && !_currentTask.IsCompletedSuccessfully);
        }

        private async Task InitializeInternalAsync()
        {
            if (_cts.IsCancellationRequested)
            {
                return;
            }

            await Task.Yield();

            using var scope = _serviceProvider.CreateScope();

            // initialize
            await scope.ServiceProvider.GetRequiredService<CatalogScanService>().InitializeAsync();
            await scope.ServiceProvider.GetRequiredService<TimerExecutionService>().InitializeAsync();
            await scope.ServiceProvider.GetRequiredService<WorkflowService>().InitializeAsync();

            // warm up
            await scope.ServiceProvider.GetRequiredService<ViewModelFactory>().GetAdminViewModelAsync();
        }

        public async Task InitializeAsync()
        {
            await _currentTask;
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            await _loopTask;
            _cts.Dispose();
        }
    }
}
