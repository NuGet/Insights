// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsTimer<T> : ICleanupOrphanRecordsTimer, ITimer where T : ICleanupOrphanCsvRecord
    {
        private readonly ICleanupOrphanRecordsService<T> _service;
        private readonly ICleanupOrphanRecordsAdapter<T> _adapter;

        public CleanupOrphanRecordsTimer(
            ICleanupOrphanRecordsService<T> service,
            ICleanupOrphanRecordsAdapter<T> adapter)
        {
            _service = service;
            _adapter = adapter;
        }

        public string Name => _adapter.OperationName;
        public string Title => CatalogScanDriverMetadata.HumanizeCodeName(Name);
        public TimerFrequency Frequency => new TimerFrequency(TimeSpan.FromHours(4));
        public bool AutoStart => false;
        public bool IsEnabled => true;
        public bool CanAbort => false;
        public bool CanDestroy => false;

        public Task AbortAsync()
        {
            throw new NotSupportedException();
        }

        public Task DestroyAsync()
        {
            throw new NotSupportedException();
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
