// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessTimer : ITimer
    {
        private readonly TimedReprocessService _service;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public TimedReprocessTimer(
            TimedReprocessService service,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _service = service;
            _options = options;
        }

        public string Name => "TimedReprocess";
        public TimeSpan Frequency => _options.Value.TimedReprocessFrequency;
        public bool AutoStart => _options.Value.AutoStartTimedReprocess;
        public bool IsEnabled => _options.Value.TimedReprocessIsEnabled;
        public int Order => 10;
        public bool CanAbort => throw new NotImplementedException();
        public bool CanDestroy => throw new NotImplementedException();

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

        public Task DestroyAsync()
        {
            throw new NotImplementedException();
        }

        public Task AbortAsync()
        {
            throw new NotImplementedException();
        }
    }
}
