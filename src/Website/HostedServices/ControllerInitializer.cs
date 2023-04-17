// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Insights.Worker;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Website
{
    public class ControllerInitializer
    {
        private readonly Lazy<Task> _initalize;

        public ControllerInitializer(
            CatalogScanService catalogScanService,
            TimerExecutionService timerExecutionService,
            WorkflowService workflowService,
            ViewModelFactory viewModelFactory)
        {
            _initalize = new Lazy<Task>(async () =>
            {
                await catalogScanService.InitializeAsync();
                await timerExecutionService.InitializeAsync();
                await workflowService.InitializeAsync();

                // warm up
                await viewModelFactory.GetAdminViewModelAsync();
            });
        }

        public async Task InitializeAsync()
        {
            await _initalize.Value;
        }
    }
}
