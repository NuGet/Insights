// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Insights.Worker
{
    public class KustoFactAttribute : FactAttribute
    {
        public KustoFactAttribute()
        {
            if (TestSettings.IsStorageEmulator)
            {
                Skip = "This Fact is skipped because the storage emulator is being used.";
            }

            if (TestSettings.KustoConnectionString is null || TestSettings.KustoDatabaseName is null)
            {
                Skip = "This Fact is skipped because the Kusto environment variables are not set. " +
                    $"Set both {TestSettings.KustoConnectionStringEnv} and {TestSettings.KustoDatabaseNameEnv}.";
            }
        }
    }
}
