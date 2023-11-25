// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessTimer : ITimer
    {
        public static string TimerName => "TimedReprocess";

        private readonly TimedReprocessService _service;
        private readonly TimedReprocessStorageService _storageService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public TimedReprocessTimer(
            TimedReprocessService service,
            TimedReprocessStorageService storageService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _service = service;
            _storageService = storageService;
            _options = options;
        }

        public string Name => TimerName;
        public TimeSpan Frequency => _options.Value.TimedReprocessFrequency;
        public bool AutoStart => _options.Value.AutoStartTimedReprocess;
        public bool IsEnabled => _options.Value.TimedReprocessIsEnabled;
        public bool CanAbort => true;
        public bool CanDestroy => true;

        public async Task InitializeAsync()
        {
            await _service.InitializeAsync();
        }

        public async Task<bool> IsRunningAsync()
        {
            return await _service.IsAnyTimedReprocessRunningAsync();
        }

        public async Task<bool> ExecuteAsync()
        {
            var run = await _service.StartAsync();
            return run is not null;
        }

        public async Task DestroyAsync()
        {
            await _storageService.ResetBucketsAsync();
        }

        public async Task AbortAsync()
        {
            await _service.AbortAsync();
        }
    }
}
