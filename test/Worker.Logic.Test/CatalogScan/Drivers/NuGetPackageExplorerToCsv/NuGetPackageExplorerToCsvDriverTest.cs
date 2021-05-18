// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGetPe;
using Xunit;
using Xunit.Abstractions;

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
        public async Task EmptyFileListIsNothingToValidate()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.18.00.13.47/microsoft.netcore.platforms.1.1.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.NETCore.Platforms",
                PackageVersion = "1.1.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.NothingToValidate, record.ResultType);
            Assert.Equal(SymbolValidationResult.NothingToValidate, record.SourceLinkResult);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.NothingToValidate, file.ResultType);
            Assert.Null(file.Path);
        }

        [Fact]
        public async Task UnsupportedFrameworksAreInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.18.14.04.32/abstractultragrid.1.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "AbstractUltraGrid",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyReferencesGroupIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.14.09.12.34/adon-models.1.0.1.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "adon-models",
                PackageVersion = "1.0.1",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task BrokenContentFilePathIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.05.06.14.58.12/amazon.lambda.tools.3.2.1.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Amazon.Lambda.Tools",
                PackageVersion = "3.2.1",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task DependenciesWithBothGroupAndDependencyIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.01.02.23.26.52/arex388.aspnet.mvc.startup.1.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Arex388.AspNet.Mvc.Startup",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyAuthorIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.11.01.14.21/aspnetcore.identity.ravendb.1.0.0-alpha2.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "AspNetCore.Identity.RavenDB",
                PackageVersion = "1.0.0-alpha2",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task RequiredLicenseAcceptanceWithoutLicenseIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.08.01.34.32/brooksoft.appsjs.1.1.2.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "brooksoft.appsjs",
                PackageVersion = "1.1.2",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task Windows1252NuspecEncodingIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.10.05.41.04/caching.dll.1.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Caching.dll",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task UnrecognizedLicenseExpressionIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.01.17.14.50.11/callisto.binder.net.0.1.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Callisto.Binder.Net",
                PackageVersion = "0.1.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyLicenseUrlIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.04.15.15.33.51/cslib.fody.1.0.4.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "CsLib.Fody",
                PackageVersion = "1.0.4",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyDescriptionIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.13.20.49.08/entropyextension.core.1.0.0-beta-3.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "EntropyExtension.Core",
                PackageVersion = "1.0.0-beta-3",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyIconUrlIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.13.23.54.34/safetynet.uimaps.webdriver.3.141.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "SafetyNet.UIMaps.WebDriver",
                PackageVersion = "3.141.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task MissingAuthorsIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.19.15.33.55/manikjindal.2.3.1.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "ManikJindal",
                PackageVersion = "2.3.1",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task InvalidDependencyVersionRangeIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.09.00.57.44/masstransit.ravendbintegration.3.2.2.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "MassTransit.RavenDbIntegration",
                PackageVersion = "3.2.2",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task DuplicateDependencyIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.11.26.10.59.56/paket.core.5.237.1.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Paket.Core",
                PackageVersion = "5.237.1",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task UppercaseBooleanForRequireLicenseAcceptanceIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.18.13.09.18/testpackage19fa75eb-9384-4371-9f82-0f4348c0aad3.1.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "TestPackage19fa75eb-9384-4371-9f82-0f4348c0aad3",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task InvalidAssemblyReferenceIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.19.16.49.43/xtoappdev.1.2.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "XTOAppDev",
                PackageVersion = "1.2.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }

        [Fact]
        public async Task EmptyProjectUrlIsInvalidMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.12.21.59.26/yahooexchangeapi.1.1.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "YahooExchangeApi",
                PackageVersion = "1.1.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Set1.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, record.ResultType);
            var file = Assert.Single(output.Value.Set2.Records);
            Assert.Equal(NuGetPackageExplorerResultType.InvalidMetadata, file.ResultType);
        }
    }
}
