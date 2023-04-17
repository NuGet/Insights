// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Website
{
    public class InitializerHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InitializerHostedService> _logger;

        public InitializerHostedService(IServiceProvider serviceProvider, ILogger<InitializerHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

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
