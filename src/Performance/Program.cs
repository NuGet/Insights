// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using BenchmarkDotNet.Running;

namespace NuGet.Insights.Performance;

internal class Program
{
    private static async Task Main(string[] args)
    {
        await Task.Yield();
        RunBenchmark();
        // await DebugAsync();
    }

    private static async Task DebugAsync()
    {
        for (var i = 0; i < 1; i++)
        {
            var test = new AuxiliaryFileUpdaterProcessor_PackageDownloads();
            test.N = 500_000;
            test.LoggerFactory = LoggerFactory.Create(x => x.AddMinimalConsole());
            await test.SetupAsync();
            await test.Baseline();
        }
    }

    private static void RunBenchmark()
    {
        var summary = BenchmarkRunner.Run<AuxiliaryFileUpdaterProcessor_PackageDownloads>();
    }
}
