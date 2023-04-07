// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using McMaster.Extensions.CommandLineUtils;
using MessagePack;
using static NuGet.Insights.Worker.BuildVersionSet.VersionSetService;

namespace NuGet.Insights.Tool
{
    public class SandboxCommand : ICommand
    {
        public SandboxCommand()
        {
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Yield();

            var summary = BenchmarkRunner.Run<VersionSetServiceTest>();
        }

        [MemoryDiagnoser]
        [SimpleJob(1, 2, 6, 1)]
        public class VersionSetServiceTest
        {
            public async Task RunAsync<T>(bool withStringIntern)
            {
                var blob = new BlockBlobClient(
                    new Uri(""),
                    new AzureSasCredential(""));
                using BlobDownloadInfo info = await blob.DownloadAsync();
                var data = await MessagePackSerializer.DeserializeAsync<Versions<T>>(
                    info.Content,
                    withStringIntern ? NuGetInsightsMessagePack.OptionsWithStringIntern : NuGetInsightsMessagePack.Options);
            }

            [GlobalSetup]
            public async Task SetupAsync()
            {
                await Unsorted_StringIntern();
            }

            [Benchmark]
            public async Task Sorted_StringIntern()
            {
                await RunAsync<CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>>>(withStringIntern: true);
            }

            [Benchmark]
            public async Task Unsorted_StringIntern()
            {
                await RunAsync<CaseInsensitiveDictionary<CaseInsensitiveDictionary<bool>>>(withStringIntern: true);
            }

            [Benchmark]
            public async Task Sorted_NoStringIntern()
            {
                await RunAsync<CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>>>(withStringIntern: false);
            }

            [Benchmark]
            public async Task Unsorted_NoStringIntern()
            {
                await RunAsync<CaseInsensitiveDictionary<CaseInsensitiveDictionary<bool>>>(withStringIntern: false);
            }
        }
    }
}
