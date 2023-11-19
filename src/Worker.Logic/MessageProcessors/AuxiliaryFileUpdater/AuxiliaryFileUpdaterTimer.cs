// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public class AuxiliaryFileUpdaterTimer<T> : IAuxiliaryFileUpdaterTimer, ITimer where T : IAsOfData
    {
        private readonly IAuxiliaryFileUpdaterService<T> _service;
        private readonly IAuxiliaryFileUpdater<T> _updater;

        public AuxiliaryFileUpdaterTimer(
            IAuxiliaryFileUpdaterService<T> service,
            IAuxiliaryFileUpdater<T> updater)
        {
            _service = service;
            _updater = updater;
        }

        public string Name => _updater.OperationName;
        public TimeSpan Frequency => _updater.Frequency;
        public bool IsEnabled => _service.HasRequiredConfiguration;
        public bool AutoStart => _updater.AutoStart;
        public bool CanAbort => false;
        public bool CanDestroy => true;

        public Task AbortAsync()
        {
            throw new NotSupportedException();
        }

        public async Task DestroyAsync()
        {
            await _service.DestroyAsync();
        }

        public async Task<bool> ExecuteAsync()
        {
            return await _service.StartAsync();
        }

        public async Task InitializeAsync()
        {
            await _service.InitializeAsync();
        }

        public async Task<bool> IsRunningAsync()
        {
            return await _service.IsRunningAsync();
        }
    }
}
