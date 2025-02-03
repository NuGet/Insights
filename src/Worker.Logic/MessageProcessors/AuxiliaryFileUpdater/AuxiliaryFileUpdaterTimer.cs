// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public class AuxiliaryFileUpdaterTimer<TInput, TRecord> : IAuxiliaryFileUpdaterTimer, ITimer
        where TInput : IAsOfData
        where TRecord : IAuxiliaryFileCsvRecord<TRecord>
    {
        private readonly IAuxiliaryFileUpdaterService<TRecord> _service;
        private readonly IAuxiliaryFileUpdater<TInput, TRecord> _updater;

        public AuxiliaryFileUpdaterTimer(
            IAuxiliaryFileUpdaterService<TRecord> service,
            IAuxiliaryFileUpdater<TInput, TRecord> updater)
        {
            _service = service;
            _updater = updater;
        }

        public string Name => _updater.OperationName;
        public string Title => _updater.Title;
        public TimeSpan Frequency => _updater.Frequency;
        public bool IsEnabled => _updater.HasRequiredConfiguration;
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
