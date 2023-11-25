// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Website
{
    public class InitializerHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<InitializerHostedService> _logger;
        private readonly TaskCompletionSource _taskCompletionSource;

        public InitializerHostedService(
            IServiceProvider serviceProvider,
            ITelemetryClient telemetryClient,
            ILogger<InitializerHostedService> logger)
        {
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

            try
            {
                using (var operation = _telemetryClient.StartOperation(nameof(InitializerHostedService)))
                {
                    var types = new List<Type>
                    {
                        typeof(ControllerInitializer)
                    };

                    var initializedTypes = new HashSet<Type>();
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        foreach (var type in types)
                        {
                            stoppingToken.ThrowIfCancellationRequested();

                            await InitializeAsync(scope.ServiceProvider, initializedTypes, type, stoppingToken);
                        }
                    }
                }
            }
            finally
            {
                _taskCompletionSource.TrySetResult();
            }
        }

        private async Task InitializeAsync(
            IServiceProvider serviceProvider,
            HashSet<Type> initializedTypes,
            Type serviceType,
            CancellationToken stoppingToken)
        {
            var initializeMethod = serviceType.GetMethod(
                "InitializeAsync",
                BindingFlags.Instance | BindingFlags.Public,
                Array.Empty<Type>());
            var hasInitializeAsync = initializeMethod is not null
                && initializeMethod.ReturnType.IsAssignableTo(typeof(Task));
            if (!hasInitializeAsync)
            {
                return;
            }

            if (!initializedTypes.Add(serviceType))
            {
                return;
            }

            IEnumerable<object> services;
            try
            {
                services = serviceProvider.GetServices(serviceType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get {Type} services from the service provider.", serviceType);
                return;
            }

            foreach (var service in services)
            {
                stoppingToken.ThrowIfCancellationRequested();
                try
                {
                    await (Task)initializeMethod.Invoke(service, Array.Empty<object>());
                    _logger.LogInformation("Successfully initialized {Type} service.", service.GetType());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get {Type} services from the service provider.", serviceType);
                    continue;
                }
            }
        }
    }
}
