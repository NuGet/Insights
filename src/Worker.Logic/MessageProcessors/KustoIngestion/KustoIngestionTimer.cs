// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionTimer : ITimer
    {
        public static string TimerName => "KustoIngestion";

        private readonly KustoIngestionService _kustoIngestionService;
        private readonly KustoIngestionStorageService _kustoIngestionStorageService;

        public KustoIngestionTimer(
            KustoIngestionService kustoIngestionService,
            KustoIngestionStorageService kustoIngestionStorageService)
        {
            _kustoIngestionService = kustoIngestionService;
            _kustoIngestionStorageService = kustoIngestionStorageService;
        }

        public string Name => TimerName;
        public TimeSpan Frequency => TimeSpan.FromDays(1);
        public bool AutoStart => false;
        public bool IsEnabled => _kustoIngestionService.HasRequiredConfiguration;
        public int Order => 40;
        public bool CanAbort => true;

        public async Task InitializeAsync()
        {
            await _kustoIngestionService.InitializeAsync();
        }

        public async Task<bool> ExecuteAsync()
        {
            var ingestion = await _kustoIngestionService.StartAsync();
            return ingestion is not null;
        }

        public async Task<bool> IsRunningAsync()
        {
            return await _kustoIngestionStorageService.IsIngestionRunningAsync();
        }

        public async Task AbortAsync()
        {
            await _kustoIngestionService.AbortAsync();
        }
    }
}
