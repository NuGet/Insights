// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageCompatibilityToCsv
{
    public class PackageCompatibilityToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageCompatibilityToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<PackageCompatibility> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageCompatibility>>();

        [Fact]
        public async Task SimplePackage()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.05.23.43.05/microsoft.codecoverage.1.0.3.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "1.0.3",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
            Assert.False(record.HasError);
            Assert.False(record.DoesNotRoundTrip);
            Assert.False(record.HasAny);
            Assert.False(record.HasUnsupported);
            Assert.False(record.HasAgnostic);
            Assert.Equal("[]", record.BrokenFrameworks);
            Assert.Equal("[\"netstandard1.0\"]", record.NuspecReader);
            Assert.Equal("[\"netstandard1.0\"]", record.NU1202);
            Assert.Equal("[\"netstandard1.0\"]", record.NuGetGallery);
            Assert.Equal("[\"netstandard1.0\"]", record.NuGetGalleryEscaped);
        }

        [Fact]
        public async Task ComplexPackage()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.15.01.02.18/newtonsoft.json.11.0.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Newtonsoft.Json",
                PackageVersion = "11.0.1",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
            Assert.False(record.HasError);
            Assert.False(record.DoesNotRoundTrip);
            Assert.False(record.HasAny);
            Assert.False(record.HasUnsupported);
            Assert.False(record.HasAgnostic);
            Assert.Equal("[]", record.BrokenFrameworks);
            Assert.Equal("[\"net20\",\"net35\",\"net40\",\"net45\",\"portable-net45\\u002Bwin8\\u002Bwp8\\u002Bwpa81\",\"portable-net40\\u002Bsl5\\u002Bwin8\\u002Bwp8\\u002Bwpa81\",\"netstandard1.0\",\"netstandard1.3\",\"netstandard2.0\"]", record.NuspecReader);
            Assert.Equal("[\"net20\",\"net35\",\"net40\",\"net45\",\"portable-net45\\u002Bwin8\\u002Bwp8\\u002Bwpa81\",\"portable-net40\\u002Bsl5\\u002Bwin8\\u002Bwp8\\u002Bwpa81\",\"netstandard1.0\",\"netstandard1.3\",\"netstandard2.0\"]", record.NU1202);
            Assert.Equal("[\"net20\",\"net35\",\"net40\",\"net45\",\"portable-net45\\u002Bwin8\\u002Bwp8\\u002Bwpa81\",\"portable-net40\\u002Bsl5\\u002Bwin8\\u002Bwp8\\u002Bwpa81\",\"netstandard1.0\",\"netstandard1.3\",\"netstandard2.0\"]", record.NuGetGallery);
            Assert.Equal("[\"net20\",\"net35\",\"net40\",\"net45\",\"portable-net45\\u002Bwin8\\u002Bwp8\\u002Bwpa81\",\"portable-net40\\u002Bsl5\\u002Bwin8\\u002Bwp8\\u002Bwpa81\",\"netstandard1.0\",\"netstandard1.3\",\"netstandard2.0\"]", record.NuGetGalleryEscaped);
        }

        [Fact]
        public async Task InvalidPortable()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.30.01.37.28/chatwork.api.0.3.2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Chatwork.Api",
                PackageVersion = "0.3.2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
            Assert.True(record.HasError);
            Assert.False(record.HasAny);
            Assert.False(record.HasUnsupported);
            Assert.False(record.HasAgnostic);
            Assert.False(record.DoesNotRoundTrip);
            Assert.Equal("[]", record.BrokenFrameworks);
            Assert.Null(record.NuspecReader);
            Assert.Null(record.NU1202);
            Assert.Null(record.NuGetGallery);
            Assert.Null(record.NuGetGalleryEscaped);
        }

        [Fact]
        public async Task UnsupportedFramework()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.07.17.13.16.14/lagovista.useradmin.rest.3.0.1522.906.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "LagoVista.UserAdmin.Rest",
                PackageVersion = "3.0.1522.906",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
            Assert.False(record.HasError);
            Assert.False(record.HasAny);
            Assert.True(record.HasUnsupported);
            Assert.False(record.HasAgnostic);
            Assert.True(record.DoesNotRoundTrip);
            Assert.Equal("[\"xmldocs\",\"xmldocs,Version=v0.0\"]", record.BrokenFrameworks);
            Assert.Equal("[\"net5.0\"]", record.NuspecReader);
            Assert.Equal("[\"net5.0\",\"unsupported\"]", record.NU1202);
            Assert.Equal("[\"net5.0\",\"unsupported\"]", record.NuGetGallery);
            Assert.Equal("[\"net5.0\",\"unsupported\"]", record.NuGetGalleryEscaped);
        }

        [Fact]
        public async Task BrokenFrameworkDueToPercent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.07.00.07.31/bbv.common.windows.7.1.1187.412.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "bbv.Common.Windows",
                PackageVersion = "7.1.1187.412",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
            Assert.False(record.HasError);
            Assert.True(record.DoesNotRoundTrip);
            Assert.True(record.HasAny);
            Assert.False(record.HasUnsupported);
            Assert.False(record.HasAgnostic);
            Assert.Equal("[\".Net 4.0,Version=v0.0\",\".Net%204.0,Version=v0.0\",\"net2040\"]", record.BrokenFrameworks);
            Assert.Equal("[\"any\"]", record.NuspecReader);
            Assert.Equal("[\"net40\"]", record.NU1202);
            Assert.Equal("[\"net40\"]", record.NuGetGallery);
            Assert.Equal("[\"net204\"]", record.NuGetGalleryEscaped);
        }

        [Fact]
        public async Task BrokenFrameworkDueToSpace()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.04.18.18.06.11/base4entity.2.1.8.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Base4Entity",
                PackageVersion = "2.1.8",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
            Assert.False(record.HasError);
            Assert.True(record.DoesNotRoundTrip);
            Assert.True(record.HasAny);
            Assert.False(record.HasUnsupported);
            Assert.False(record.HasAgnostic);
            Assert.Equal("[\".NET Framework 4.6.1,Version=v0.0\",\"netframework461\"]", record.BrokenFrameworks);
            Assert.Equal("[\"any\"]", record.NuspecReader);
            Assert.Equal("[\"net461\"]", record.NU1202);
            Assert.Equal("[\"net461\"]", record.NuGetGallery);
            Assert.Equal("[\"net461\"]", record.NuGetGalleryEscaped);
        }

        [Fact]
        public async Task BrokenFrameworkDueToUnderscore()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.23.01.41.56/abb.ahc.models.api.5.0.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "ABB.AHC.Models.API",
                PackageVersion = "5.0.1",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
            Assert.False(record.HasError);
            Assert.True(record.DoesNotRoundTrip);
            Assert.True(record.HasAny);
            Assert.False(record.HasUnsupported);
            Assert.False(record.HasAgnostic);
            Assert.Equal("[\"NETSTANDARD_20,Version=v0.0\",\"netstandard20\"]", record.BrokenFrameworks);
            Assert.Equal("[\"any\",\"net40\",\"net45\",\"net46\"]", record.NuspecReader);
            Assert.Equal("[\"net\",\"net40\",\"net45\",\"net46\",\"netstandard2.0\"]", record.NU1202);
            Assert.Equal("[\"net\",\"net40\",\"net45\",\"net46\",\"netstandard2.0\"]", record.NuGetGallery);
            Assert.Equal("[\"net\",\"net40\",\"net45\",\"net46\",\"netstandard2.0\"]", record.NuGetGalleryEscaped);
        }
    }
}
