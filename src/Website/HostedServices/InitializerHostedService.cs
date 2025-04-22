// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Website
{
    public class InitializerHostedService : BackgroundService
    {
        private readonly IReadOnlyList<(Type Type, Func<IServiceProvider, Task> InitializeAsync)> _servicesToInitialize;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<InitializerHostedService> _logger;
        private readonly TaskCompletionSource _taskCompletionSource;

        public InitializerHostedService(
            IServiceProvider serviceProvider,
            ITelemetryClient telemetryClient,
            ILogger<InitializerHostedService> logger)
        {
            _servicesToInitialize = [
                (typeof(ViewModelFactory), x => x.GetRequiredService<ViewModelFactory>().InitializeAsync()),
            ];
            _serviceProvider = serviceProvider;
            _telemetryClient = telemetryClient;
            _logger = logger;
            _taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task WaitAsync()
        {
            return _taskCompletionSource.Task;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

            bool allSucceeded;
            do
            {
                allSucceeded = await InitializeAsync(warmUp: true, stoppingToken);
                if (!allSucceeded)
                {
                    var sleep = TimeSpan.FromSeconds(2);
                    _logger.LogWarning("Failed to initialize some services. Waiting {Sleep} seconds before retrying.", sleep);
                    await Task.Delay(sleep, stoppingToken);
                }
            }
            while (!stoppingToken.IsCancellationRequested && !allSucceeded);

            _taskCompletionSource.TrySetResult();
        }

        public async Task<bool> InitializeAsync(bool warmUp, CancellationToken token)
        {
            using var operation = _telemetryClient.StartOperation(nameof(InitializerHostedService));
            using var scope = _serviceProvider.CreateScope();

            // initialize
            bool allSucceeded = true;
            foreach (var (type, initializeServiceAsync) in _servicesToInitialize)
            {
                token.ThrowIfCancellationRequested();
                allSucceeded &= await InitializeServiceAsync(scope.ServiceProvider, type, initializeServiceAsync, token);
            }

            if (warmUp && allSucceeded)
            {
                // warm up
                _logger.LogInformation("Successfully initialized all services. Warming up the admin view.");
                await scope.ServiceProvider.GetRequiredService<ViewModelFactory>().GetAdminViewModelAsync();
            }

            return allSucceeded;
        }

        private async Task<bool> InitializeServiceAsync(
            IServiceProvider serviceProvider,
            Type type,
            Func<IServiceProvider, Task> initializeServiceAsync,
            CancellationToken token)
        {
            try
            {
                await initializeServiceAsync(serviceProvider);
                _logger.LogInformation("Successfully initialized {Type} service.", type.FullName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize {Type} service.", type.FullName);
                return false;
            }
        }
    }
}
