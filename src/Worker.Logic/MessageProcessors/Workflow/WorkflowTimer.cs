// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowTimer : ITimer
    {
        private readonly WorkflowService _service;

        public WorkflowTimer(WorkflowService service)
        {
            _service = service;
        }

        public string Name => "Workflow";
        public TimeSpan Frequency => TimeSpan.FromDays(1);
        public bool AutoStart => false;
        public bool IsEnabled => _service.HasRequiredConfiguration;
        public int Order => default;

        public async Task<bool> ExecuteAsync()
        {
            var workflow = await _service.StartAsync(maxCommitTimestamp: null);
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
    }
}
