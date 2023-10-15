// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageLicenseToCsv
{
    public class PackageLicenseToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageLicenseToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<PackageLicense> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageLicense>>();

        [Fact]
        public async Task HandlesLicenseFile()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2023.04.12.20.09.12/libgit2sharp.0.27.2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "LibGit2Sharp",
                PackageVersion = "0.27.2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageLicenseResultType.File, record.ResultType);
            Assert.Equal("https://aka.ms/deprecateLicenseUrl", record.Url);
            Assert.Null(record.Expression);
            Assert.Equal("App_Readme/LICENSE.md", record.File);
            Assert.Equal("https://aka.ms/deprecateLicenseUrl", record.GeneratedUrl);
            Assert.Null(record.ExpressionParsed);
            Assert.Null(record.ExpressionLicenses);
            Assert.Null(record.ExpressionExceptions);
            Assert.Null(record.ExpressionNonStandardLicenses);
            Assert.Null(record.ExpressionHasDeprecatedIdentifier);
            Assert.Equal(1081, record.FileSize);
            Assert.Equal("10UzT325N2HPnB/9TAPQqENhj/h/WUB3vWAppz3QzLI=", record.FileSHA256);
            Assert.StartsWith("The MIT License\n\nCopyright (c) LibGit2Sharp contributors\n\nPermission is hereby granted", record.FileContent);
        }

        [Fact]
        public async Task HandlesComplexLicenseExpression()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2023.05.26.19.15.06/fgs.reflection.extensions.2020.8.3.2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "FGS.Reflection.Extensions",
                PackageVersion = "2020.8.3.2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageLicenseResultType.Expression, record.ResultType);
            Assert.Equal("https://licenses.nuget.org/(Apache-2.0%20OR%20MS-PL)%20AND%20MIT%20AND%20BSD-3-Clause", record.Url);
            Assert.Equal("(Apache-2.0 OR MS-PL) AND MIT AND BSD-3-Clause", record.Expression);
            Assert.Null(record.File);
            Assert.Equal("https://licenses.nuget.org/(Apache-2.0%20OR%20MS-PL)%20AND%20MIT%20AND%20BSD-3-Clause", record.GeneratedUrl);
            Assert.Equal(
                "{" +
                "\"Left\":{" +
                    "\"Left\":{" +
                        "\"Left\":{" +
                            "\"Identifier\":\"Apache-2.0\"," +
                            "\"IsStandardLicense\":true," +
                            "\"Plus\":false," +
                            "\"Type\":\"License\"}," +
                        "\"LogicalOperatorType\":\"Or\"," +
                        "\"OperatorType\":\"LogicalOperator\"," +
                        "\"Right\":{" +
                            "\"Identifier\":\"MS-PL\"," +
                            "\"IsStandardLicense\":true," +
                            "\"Plus\":false," +
                            "\"Type\":\"License\"" +
                        "}," +
                        "\"Type\":\"Operator\"}," +
                    "\"LogicalOperatorType\":\"And\"," +
                    "\"OperatorType\":\"LogicalOperator\"," +
                    "\"Right\":{" +
                        "\"Identifier\":\"MIT\"," +
                        "\"IsStandardLicense\":true," +
                        "\"Plus\":false," +
                        "\"Type\":\"License\"" +
                    "}," +
                    "\"Type\":\"Operator\"}," +
                "\"LogicalOperatorType\":\"And\"," +
                "\"OperatorType\":\"LogicalOperator\"," +
                "\"Right\":{" +
                    "\"Identifier\":\"BSD-3-Clause\"," +
                    "\"IsStandardLicense\":true," +
                    "\"Plus\":false," +
                    "\"Type\":\"License\"}," +
                "\"Type\":\"Operator\"}",
                record.ExpressionParsed);
            Assert.Equal("[\"Apache-2.0\",\"BSD-3-Clause\",\"MIT\",\"MS-PL\"]", record.ExpressionLicenses);
            Assert.Equal("[]", record.ExpressionExceptions);
            Assert.Equal("[]", record.ExpressionNonStandardLicenses);
            Assert.False(record.ExpressionHasDeprecatedIdentifier);
            Assert.Null(record.FileSize);
            Assert.Null(record.FileSHA256);
            Assert.Null(record.FileContent);
        }

        [Fact]
        public async Task HandlesDeprecatedLicenseIdentifierExpression()
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
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageLicenseResultType.Expression, record.ResultType);
            Assert.Equal("https://licenses.nuget.org/BSD-2-Clause-FreeBSD", record.Url);
            Assert.Equal("BSD-2-Clause-FreeBSD", record.Expression);
            Assert.Null(record.File);
            Assert.Null(record.GeneratedUrl);
            Assert.Null(record.ExpressionParsed);
            Assert.Null(record.ExpressionLicenses);
            Assert.Null(record.ExpressionExceptions);
            Assert.Null(record.ExpressionNonStandardLicenses);
            Assert.True(record.ExpressionHasDeprecatedIdentifier);
            Assert.Null(record.FileSize);
            Assert.Null(record.FileSHA256);
            Assert.Null(record.FileContent);
        }

        [Fact]
        public async Task HandlesSimpleLicenseExpression()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2023.02.02.16.14.46/knapcode.torsharp.2.13.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "2.13.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageLicenseResultType.Expression, record.ResultType);
            Assert.Equal("https://licenses.nuget.org/MIT", record.Url);
            Assert.Equal("MIT", record.Expression);
            Assert.Null(record.File);
            Assert.Equal("https://licenses.nuget.org/MIT", record.GeneratedUrl);
            Assert.Equal("{\"Identifier\":\"MIT\",\"IsStandardLicense\":true,\"Plus\":false,\"Type\":\"License\"}", record.ExpressionParsed);
            Assert.Equal("[\"MIT\"]", record.ExpressionLicenses);
            Assert.Equal("[]", record.ExpressionExceptions);
            Assert.Equal("[]", record.ExpressionNonStandardLicenses);
            Assert.False(record.ExpressionHasDeprecatedIdentifier);
            Assert.Null(record.FileSize);
            Assert.Null(record.FileSHA256);
            Assert.Null(record.FileContent);
        }

        [Fact]
        public async Task HandlesExceptionInLicenseExpression()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2023.01.25.21.41.37/wasmtime.5.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Wasmtime",
                PackageVersion = "5.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageLicenseResultType.Expression, record.ResultType);
            Assert.Equal("https://licenses.nuget.org/Apache-2.0%20WITH%20LLVM-exception", record.Url);
            Assert.Equal("Apache-2.0 WITH LLVM-exception", record.Expression);
            Assert.Null(record.File);
            Assert.Equal("https://licenses.nuget.org/Apache-2.0%20WITH%20LLVM-exception", record.GeneratedUrl);
            Assert.Equal(
                "{" +
                "\"Exception\":{" +
                    "\"Identifier\":\"LLVM-exception\"}," +
                "\"License\":{" +
                    "\"Identifier\":\"Apache-2.0\"," +
                    "\"IsStandardLicense\":true," +
                    "\"Plus\":false," +
                    "\"Type\":\"License\"}," +
                "\"OperatorType\":\"WithOperator\"," +
                "\"Type\":\"Operator\"}",
                record.ExpressionParsed);
            Assert.Equal("[\"Apache-2.0\"]", record.ExpressionLicenses);
            Assert.Equal("[\"LLVM-exception\"]", record.ExpressionExceptions);
            Assert.Equal("[]", record.ExpressionNonStandardLicenses);
            Assert.False(record.ExpressionHasDeprecatedIdentifier);
            Assert.Null(record.FileSize);
            Assert.Null(record.FileSHA256);
            Assert.Null(record.FileContent);
        }

        [Fact]
        public async Task HandlesLicenseUrl()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.13.22.10.24/system.runtime.4.3.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "System.Runtime",
                PackageVersion = "4.3.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageLicenseResultType.Url, record.ResultType);
            Assert.Equal("http://go.microsoft.com/fwlink/?LinkId=329770", record.Url);
            Assert.Null(record.Expression);
            Assert.Null(record.File);
            Assert.Null(record.GeneratedUrl);
            Assert.Null(record.ExpressionParsed);
            Assert.Null(record.ExpressionLicenses);
            Assert.Null(record.ExpressionExceptions);
            Assert.Null(record.ExpressionNonStandardLicenses);
            Assert.Null(record.ExpressionHasDeprecatedIdentifier);
            Assert.Null(record.FileSize);
            Assert.Null(record.FileSHA256);
            Assert.Null(record.FileContent);
        }

        [Fact]
        public async Task HandlesNoLicense()
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
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageLicenseResultType.None, record.ResultType);
            Assert.Null(record.Url);
            Assert.Null(record.Expression);
            Assert.Null(record.File);
            Assert.Null(record.GeneratedUrl);
            Assert.Null(record.ExpressionParsed);
            Assert.Null(record.ExpressionLicenses);
            Assert.Null(record.ExpressionExceptions);
            Assert.Null(record.ExpressionNonStandardLicenses);
            Assert.Null(record.ExpressionHasDeprecatedIdentifier);
            Assert.Null(record.FileSize);
            Assert.Null(record.FileSHA256);
            Assert.Null(record.FileContent);
        }
    }
}
