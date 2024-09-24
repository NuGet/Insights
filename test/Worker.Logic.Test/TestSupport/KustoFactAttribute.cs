// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;

namespace NuGet.Insights.Worker
{
    public class KustoFactAttribute : NoInMemoryStorageFactAttribute
    {
        public KustoFactAttribute()
        {
            if (Skip is null)
            {
                if (LogicTestSettings.UseDevelopmentStorage)
                {
                    Skip = "This Fact is skipped because the storage emulator is being used.";
                }
                else if (new NuGetInsightsWorkerSettings().WithTestKustoSettings().KustoConnectionString is null)
                {
                    Skip = "This Fact is skipped because the Kusto environment variables are not set.";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Skip = "This Fact is skipped because Kusto initialization randomly hangs in macOS CI environments.";
                }
            }
        }
    }
}
