// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowTimer : ITimer
    {
        public static string TimerName => "Workflow";

        private readonly WorkflowService _service;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public WorkflowTimer(WorkflowService service, IOptions<NuGetInsightsWorkerSettings> options)
        {
            _service = service;
            _options = options;
        }

        public string Name => TimerName;
        public TimeSpan Frequency => _options.Value.WorkflowFrequency;
        public bool AutoStart => false;
        public bool IsEnabled => _service.HasRequiredConfiguration;
        public int Order => default;
        public bool CanAbort => true;
        public bool CanDestroy => false;

        public async Task AbortAsync()
        {
            await _service.AbortAsync();
        }

        public async Task<bool> ExecuteAsync()
        {
            var workflow = await _service.StartAsync();
            return workflow is not null;
        }

        public async Task InitializeAsync()
        {
            await _service.InitializeAsync();
        }

        public async Task<bool> IsRunningAsync()
        {
            return await _service.IsWorkflowRunningAsync();
        }

        public Task DestroyAsync()
        {
            throw new NotSupportedException();
        }
    }
}
