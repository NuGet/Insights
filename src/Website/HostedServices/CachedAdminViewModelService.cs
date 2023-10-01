// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Website
{
    public class CachedAdminViewModelService : BackgroundService
    {
        public class AdminViewModelCache : IAdminViewModelCache
        {
            public bool Refreshing { get; set; }
            public CachedAdminViewModel Value { get; set; }
            public NuGetInsightsWebsiteSettings Settings { get; set; }
        }

        private readonly IServiceProvider _serviceProvider;
        private readonly AdminViewModelCache _cache;
        private readonly InitializerHostedService _initializer;
        private readonly IOptions<NuGetInsightsWebsiteSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<CachedAdminViewModelService> _logger;

        public CachedAdminViewModelService(
            IServiceProvider serviceProvider,
            AdminViewModelCache cache,
            InitializerHostedService initializer,
            IOptions<NuGetInsightsWebsiteSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<CachedAdminViewModelService> logger)
        {
            _serviceProvider = serviceProvider;
            _cache = cache;
            _initializer = initializer;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Value.ShowAdminMetadata)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            while (!stoppingToken.IsCancellationRequested)
            {
                using (_telemetryClient.StartOperation(nameof(CachedAdminViewModelService)))
                {
                    await _initializer.WaitAsync();

                    _cache.Refreshing = true;
                    _logger.LogInformation("Loading latest admin view model.");
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var asOfTimestamp = DateTimeOffset.UtcNow;
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var factory = scope.ServiceProvider.GetRequiredService<ViewModelFactory>();
                            var data = await factory.GetAdminViewModelAsync();
                            _cache.Value = new CachedAdminViewModel(asOfTimestamp, data);
                            _cache.Settings = scope.ServiceProvider.GetRequiredService<IOptions<NuGetInsightsWebsiteSettings>>().Value;
                        }
                        _logger.LogInformation(
                            "Latest admin view model loaded after {DurationMs}ms. Sleeping for {SleepMs}ms.",
                            sw.Elapsed.TotalMilliseconds,
                            _options.Value.CachedAdminViewModelMaxAge.TotalMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to load admin view model after {DurationMs}ms. Sleeping for {SleepMs}ms.",
                            sw.Elapsed.TotalMilliseconds,
                            _options.Value.CachedAdminViewModelMaxAge.TotalMilliseconds);
                    }
                    finally
                    {
                        _cache.Refreshing = false;
                    }
                }

                await Task.Delay(_options.Value.CachedAdminViewModelMaxAge, stoppingToken);
            }
        }
    }
}
