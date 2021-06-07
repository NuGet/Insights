// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageAssetToCsv
{
    public class PackageAssetToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageAssetToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public ICatalogLeafToCsvDriver<PackageAsset> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageAsset>>();

        [Fact]
        public async Task CompileLibAssemblies_CompileRefAssemblies_RuntimeAssemblies()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.13.22.10.12/system.io.4.3.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "System.IO",
                PackageVersion = "4.3.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            var patternSets = output.Value.Records.Select(x => x.PatternSet);
            Assert.Contains(PatternSetType.RuntimeAssemblies, patternSets);
            Assert.Contains(PatternSetType.CompileLibAssemblies, patternSets);
            Assert.Contains(PatternSetType.CompileRefAssemblies, patternSets);
        }

        [Fact]
        public async Task ContentFiles()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.06.08.34.14/microsoft.build.runtime.15.3.409.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.Build.Runtime",
                PackageVersion = "15.3.409",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            var patternSets = output.Value.Records.Select(x => x.PatternSet).Distinct();
            Assert.Contains(PatternSetType.ContentFiles, patternSets);
        }

        [Fact]
        public async Task EmbedAssemblies()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.06.07.03.41.59/knapcode.embedinterop.0.0.1-alpha.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Knapcode.EmbedInterop",
                PackageVersion = "0.0.1-alpha",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            var patternSets = output.Value.Records.Select(x => x.PatternSet).Distinct();
            Assert.Contains(PatternSetType.EmbedAssemblies, patternSets);
        }

        [Fact]
        public async Task MSBuildFiles()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.13.22.06.38/netstandard.library.2.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "NETStandard.Library",
                PackageVersion = "2.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            var patternSets = output.Value.Records.Select(x => x.PatternSet).Distinct();
            Assert.Contains(PatternSetType.MSBuildFiles, patternSets);
        }

        [Fact]
        public async Task MSBuildMultiTargetingFiles()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.29.04.23.22/xunit.core.2.4.1.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "xunit.core",
                PackageVersion = "2.4.1",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            var patternSets = output.Value.Records.Select(x => x.PatternSet).Distinct();
            Assert.Contains(PatternSetType.MSBuildMultiTargetingFiles, patternSets);
        }

        [Fact]
        public async Task MSBuildTransitiveFiles()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.05.12.14.58.17/entityframework.6.4.4.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "EntityFramework",
                PackageVersion = "6.4.4",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            var patternSets = output.Value.Records.Select(x => x.PatternSet).Distinct();
            Assert.Contains(PatternSetType.MSBuildTransitiveFiles, patternSets);
        }

        [Fact]
        public async Task NativeLibraries()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.05.13.53.29/runtime.debian.8-x64.runtime.native.system.security.cryptography.openssl.4.3.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "runtime.debian.8-x64.runtime.native.System.Security.Cryptography.OpenSsl",
                PackageVersion = "4.3.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            var patternSets = output.Value.Records.Select(x => x.PatternSet).Distinct();
            Assert.Contains(PatternSetType.NativeLibraries, patternSets);
        }

        [Fact]
        public async Task ResourceAssemblies()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.08.15.20.22.39/microsoft.codeanalysis.common.3.3.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeAnalysis.Common",
                PackageVersion = "3.3.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            var patternSets = output.Value.Records.Select(x => x.PatternSet).Distinct();
            Assert.Contains(PatternSetType.ResourceAssemblies, patternSets);
        }

        [Fact]
        public async Task ToolsAssemblies()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.03.23.18.12/microsoft.aspnetcore.razor.design.2.2.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.AspNetCore.Razor.Design",
                PackageVersion = "2.2.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            var patternSets = output.Value.Records.Select(x => x.PatternSet).Distinct();
            Assert.Contains(PatternSetType.ToolsAssemblies, patternSets);
        }
    }
}
