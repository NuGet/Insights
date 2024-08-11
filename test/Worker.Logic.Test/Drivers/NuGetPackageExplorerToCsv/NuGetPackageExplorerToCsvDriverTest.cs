// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetPe;

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    /// <summary>
    /// Full set of problematic packages are here:
    /// https://gist.github.com/joelverhagen/852222bb623d7612448946a6b15d23c4
    /// </summary>
    public class NuGetPackageExplorerToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public NuGetPackageExplorerToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<NuGetPackageExplorerRecord, NuGetPackageExplorerFile> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<NuGetPackageExplorerRecord, NuGetPackageExplorerFile>>();

        [Fact]
        public async Task SerializesCompilerFlagsInLexOrder()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.09.15.07.06.49/npgsql.entityframeworkcore.postgresql.5.0.10.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Npgsql.EntityFrameworkCore.PostgreSQL",
                PackageVersion = "5.0.10",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(
                "{" +
                "\"compiler-version\":\"3.11.0-4.21403.6\\u002Bae1fff344d46976624e68ae17164e0607ab68b10\"," +
                "\"define\":\"TRACE,RELEASE,NETSTANDARD,NETSTANDARD2_1,NETSTANDARD1_0_OR_GREATER,NETSTANDARD1_1_OR_GREATER,NETSTANDARD1_2_OR_GREATER,NETSTANDARD1_3_OR_GREATER,NETSTANDARD1_4_OR_GREATER,NETSTANDARD1_5_OR_GREATER,NETSTANDARD1_6_OR_GREATER,NETSTANDARD2_0_OR_GREATER,NETSTANDARD2_1_OR_GREATER\"," +
                "\"language\":\"C#\"," +
                "\"language-version\":\"9.0\"," +
                "\"optimization\":\"release\"," +
                "\"output-kind\":\"DynamicallyLinkedLibrary\"," +
                "\"platform\":\"AnyCpu\"," +
                "\"runtime-version\":\"5.0.10\\u002Be1825b4928afa9455cc51e1de2b2e66c8be3018d\"," +
                "\"source-file-count\":\"163\"," +
                "\"version\":\"2\"" +
                "}",
                file.CompilerFlags);
        }

        [Fact]
        public async Task SerializesSourceUrlRepoInfoInLexOrder()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2023.05.04.03.59.31/microsoft.extensions.options.2.2.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.Extensions.Options",
                PackageVersion = "2.2.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records.Where(x => x.Extension == ".dll"));
            Assert.Equal(
                "[{" +
                "\"Example\":\"https://raw.githubusercontent.com/aspnet/Extensions/9bc79b2f25a3724376d7af19617c33749a30ea3a/src/Options/Options/src/OptionsServiceCollectionExtensions.cs\"," +
                "\"FileCount\":16," +
                "\"Repo\":{" +
                    "\"Owner\":\"aspnet\"," +
                    "\"Ref\":\"9bc79b2f25a3724376d7af19617c33749a30ea3a\"," +
                    "\"Repo\":\"Extensions\"," +
                    "\"Type\":\"GitHub\"}}]",
                file.SourceUrlRepoInfo);
        }

        [Fact]
        public async Task EmptyFileListIsNothingToValidate()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.18.00.13.47/microsoft.netcore.platforms.1.1.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.NETCore.Platforms",
                PackageVersion = "1.1.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.NothingToValidate, record.ResultType);
            Assert.Equal(SymbolValidationResult.NothingToValidate, record.SourceLinkResult);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.NothingToValidate, file.ResultType);
            Assert.Null(file.Path);
        }

        [Fact]
        public async Task UnsupportedFrameworksAreInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.18.14.04.32/abstractultragrid.1.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "AbstractUltraGrid",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyReferencesGroupIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.14.09.12.34/adon-models.1.0.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "adon-models",
                PackageVersion = "1.0.1",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task BrokenContentFilePathIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.05.06.14.58.12/amazon.lambda.tools.3.2.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Amazon.Lambda.Tools",
                PackageVersion = "3.2.1",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task DependenciesWithBothGroupAndDependencyIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.01.02.23.26.52/arex388.aspnet.mvc.startup.1.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Arex388.AspNet.Mvc.Startup",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyAuthorIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.11.01.14.21/aspnetcore.identity.ravendb.1.0.0-alpha2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "AspNetCore.Identity.RavenDB",
                PackageVersion = "1.0.0-alpha2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task RequiredLicenseAcceptanceWithoutLicenseIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.08.01.34.32/brooksoft.appsjs.1.1.2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "brooksoft.appsjs",
                PackageVersion = "1.1.2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task Windows1252NuspecEncodingIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.10.05.41.04/caching.dll.1.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Caching.dll",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task UnrecognizedLicenseExpressionIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.01.17.14.50.11/callisto.binder.net.0.1.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Callisto.Binder.Net",
                PackageVersion = "0.1.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyLicenseUrlIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.04.15.15.33.51/cslib.fody.1.0.4.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "CsLib.Fody",
                PackageVersion = "1.0.4",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyDescriptionIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.13.20.49.08/entropyextension.core.1.0.0-beta-3.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "EntropyExtension.Core",
                PackageVersion = "1.0.0-beta-3",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyIconUrlIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.13.23.54.34/safetynet.uimaps.webdriver.3.141.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "SafetyNet.UIMaps.WebDriver",
                PackageVersion = "3.141.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task MissingAuthorsIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.19.15.33.55/manikjindal.2.3.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "ManikJindal",
                PackageVersion = "2.3.1",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task InvalidDependencyVersionRangeIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.09.00.57.44/masstransit.ravendbintegration.3.2.2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "MassTransit.RavenDbIntegration",
                PackageVersion = "3.2.2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task DuplicateDependencyIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.11.26.10.59.56/paket.core.5.237.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Paket.Core",
                PackageVersion = "5.237.1",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task UppercaseBooleanForRequireLicenseAcceptanceIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.18.13.09.18/testpackage19fa75eb-9384-4371-9f82-0f4348c0aad3.1.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "TestPackage19fa75eb-9384-4371-9f82-0f4348c0aad3",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task InvalidAssemblyReferenceIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.19.16.49.43/xtoappdev.1.2.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "XTOAppDev",
                PackageVersion = "1.2.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyProjectUrlIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.12.21.59.26/yahooexchangeapi.1.1.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "YahooExchangeApi",
                PackageVersion = "1.1.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        /// <summary>
        /// Tracked by https://github.com/NuGetPackageExplorer/NuGetPackageExplorer/issues/1505
        /// </summary>
        [Fact]
        public async Task ToolsPropertyMissingFromDepsJsonFileIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2022.06.04.19.46.05/jetbrains.resharper.globaltools.2022.2.0-eap03.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "JetBrains.ReSharper.GlobalTools",
                PackageVersion = "2022.2.0-eap03",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }
    }
}
