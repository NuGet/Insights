// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using MessagePack;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Tool
{
    public class SandboxCommand : ICommand
    {
        private readonly IVersionSetProvider _versionSetProvider;

        public SandboxCommand(IVersionSetProvider versionSetProvider)
        {
            _versionSetProvider = versionSetProvider;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Yield();

            var bytes = MessagePackSerializer.Serialize(new CaseInsensitiveSortedDictionary<ReadableKey<int>>
            {
                { "Foo", new ReadableKey<int>("Foo", 100) },
                { "Bar", new ReadableKey<int>("Bar", 200) },
            }, NuGetInsightsMessagePack.Options);

            var output = MessagePackSerializer.Deserialize<CaseInsensitiveDictionary<ReadableKey<int>>>(bytes, NuGetInsightsMessagePack.Options);

            var json = MessagePackSerializer.ConvertToJson(bytes, NuGetInsightsMessagePack.Options);

            // var versionSet = await _versionSetProvider.GetAsync();
            // versionSet.TryGetId("newtonsoft.json", out var id);
        }
    }
}
