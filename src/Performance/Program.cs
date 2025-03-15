// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace NuGet.Insights.Performance;

internal class Program
{
    private static async Task Main(string[] args)
    {
        await Task.Yield();
        RunBenchmark();
        // await DebugPackageDownloadsAsync();
        // await DebugPackageFilesAsync();
    }

    private static void RunBenchmark()
    {
        var summary = BenchmarkRunner.Run(
            typeof(Program).Assembly,
            ManualConfig
                .Create(DefaultConfig.Instance)
                .WithOption(ConfigOptions.JoinSummary, value: true));
    }

    private static async Task DebugPackageDownloadsAsync()
    {
        for (var i = 0; i < 1; i++)
        {
            var test = new PackageDownloads_Compact_Fake();
            test.LoggerFactory = LoggerFactory.Create(x => x.AddMinimalConsole());
            await test.SetupAsync();
            await test.BigMode_MaxDivisions_AllNewRecords();
        }
    }

    private static async Task DebugPackageFilesAsync()
    {
        for (var i = 0; i < 1; i++)
        {
            var test = new PackageFiles_Compact_Real();
            test.LoggerFactory = LoggerFactory.Create(x => x.AddMinimalConsole());
            await test.SetupAsync();
            await test.BigMode_DefaultDivisions_NoNewRecords();
        }
    }
}
