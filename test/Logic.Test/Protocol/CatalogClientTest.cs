// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class CatalogClientTest : IDisposable
    {
        public class TheGetCommitTimestampAsyncMethod : CatalogClientTest
        {
            [Fact]
            public async Task ReturnsCommitTimestamp()
            {
                for (var attempt = 0;; attempt++)
                {
                    var index = await Target.GetCatalogIndexAsync();
                    var commitTimestamp = await Target.GetCommitTimestampAsync();

                    if (attempt < 5 && commitTimestamp != index.CommitTimestamp)
                    {
                        continue;
                    }

                    Assert.Equal(index.CommitTimestamp, commitTimestamp);
                    break;
                }
            }

            public TheGetCommitTimestampAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }
        }

        public class TheGetCatalogIndexAsyncMethod : CatalogClientTest
        {
            [Fact]
            public async Task ReturnsRecentUtcTimestamp()
            {
                var index = await Target.GetCatalogIndexAsync();

                Assert.Equal(TimeSpan.Zero, index.CommitTimestamp.Offset);
                Assert.InRange(index.CommitTimestamp, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            }

            public TheGetCatalogIndexAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }
        }

        public class TheGetCatalogPageAsyncMethod : CatalogClientTest
        {
            [Fact]
            public async Task ReturnsUtcTimestamp()
            {
                var page = await Target.GetCatalogPageAsync("https://api.nuget.org/v3/catalog0/page10006.json");

                Assert.Equal(TimeSpan.Zero, page.CommitTimestamp.Offset);
                Assert.Equal(DateTimeOffset.Parse("2020-04-17T04:23:09.956209Z", CultureInfo.InvariantCulture), page.CommitTimestamp);
                Assert.Equal("https://api.nuget.org/v3/catalog0/index.json", page.Parent);
                Assert.Equal(550, page.Count);

                Assert.Equal(549, page.Items.Count(x => x.LeafType == CatalogLeafType.PackageDetails));
                Assert.Equal(1, page.Items.Count(x => x.LeafType == CatalogLeafType.PackageDelete));
            }

            public TheGetCatalogPageAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }
        }

        public class TheGetCatalogLeafAsyncMethod : CatalogClientTest
        {
            [Fact]
            public async Task AllowsNullListed()
            {
                var leaf = await Target.GetCatalogLeafAsync(
                    CatalogLeafType.PackageDetails,
                    "https://api.nuget.org/v3/catalog0/data/2015.02.01.06.22.45/antixss.4.0.1.json");

                var details = Assert.IsType<PackageDetailsCatalogLeaf>(leaf);
                Assert.Null(details.Listed);
                Assert.Equal(DateTimeOffset.Parse("1900-01-01T00:00:00Z", CultureInfo.InvariantCulture), details.Published);
                Assert.False(details.IsListed());
            }

            [Fact]
            public async Task AllowsInvalidVersionRangeString()
            {
                var leaf = await Target.GetCatalogLeafAsync(
                    CatalogLeafType.PackageDetails,
                    "https://api.nuget.org/v3/catalog0/data/2016.03.14.21.19.28/servicestack.extras.serilog.2.0.1.json");

                var details = Assert.IsType<PackageDetailsCatalogLeaf>(leaf);
                var dependencyGroup = details.DependencyGroups.Single();
                var dependency = dependencyGroup.Dependencies.Single(x => x.Id == "ServiceStack.Interfaces");
                Assert.Equal("0.0.0-~4", dependency.Range);
                Assert.Equal(VersionRange.All, dependency.ParseRange());
            }

            [Fact]
            public async Task ReturnsOriginalIsPrereleaseEvenIfWrong()
            {
                var leaf = await Target.GetCatalogLeafAsync(
                    CatalogLeafType.PackageDetails,
                    "https://api.nuget.org/v3/catalog0/data/2016.03.11.21.02.55/mvid.fody.2.json");

                var details = Assert.IsType<PackageDetailsCatalogLeaf>(leaf);
                Assert.True(details.IsPrerelease);
                Assert.False(details.ParsePackageVersion().IsPrerelease);
            }

            [Fact]
            public async Task ReturnsNullForCorruptedDependencyRange()
            {
                var leaf = await Target.GetCatalogLeafAsync(
                    CatalogLeafType.PackageDetails,
                    "https://api.nuget.org/v3/catalog0/data/2016.02.21.11.06.01/dingu.generic.repo.ef7.1.0.0-beta2.json");

                var details = Assert.IsType<PackageDetailsCatalogLeaf>(leaf);
                var dependencyGroup = details.DependencyGroups.Single(x => x.TargetFramework == ".NETPlatform5.4");
                var dependency = dependencyGroup.Dependencies.Single(x => x.Id == "System.Runtime");
                Assert.Null(dependency.Range);
            }

            [Fact]
            public async Task ReturnsOriginalIsPrerelease()
            {
                var leaf = await Target.GetCatalogLeafAsync(
                    CatalogLeafType.PackageDetails,
                    "https://api.nuget.org/v3/catalog0/data/2016.03.11.21.02.55/mvid.fody.2.json");

                var details = Assert.IsType<PackageDetailsCatalogLeaf>(leaf);
                Assert.True(details.IsPrerelease);
                Assert.False(details.ParsePackageVersion().IsPrerelease);
            }

            [Fact]
            public async Task ParsesDetailsLeaf()
            {
                var leaf = await Target.GetCatalogLeafAsync(
                    CatalogLeafType.PackageDetails,
                    "https://api.nuget.org/v3/catalog0/data/2021.03.22.20.13.54/newtonsoft.json.13.0.1.json");

                Assert.Equal(CatalogLeafType.PackageDetails, leaf.LeafType);
                var details = Assert.IsType<PackageDetailsCatalogLeaf>(leaf);
                Assert.Equal(DateTimeOffset.Parse("2021-03-22T20:10:49.407Z", CultureInfo.InvariantCulture), details.Created);
            }

            [Fact]
            public async Task ParsesDeleteLeaf()
            {
                var leaf = await Target.GetCatalogLeafAsync(
                    CatalogLeafType.PackageDelete,
                    "https://api.nuget.org/v3/catalog0/data/2020.04.17.01.07.02/microsoft.aspnetcore.components.webassembly.build.3.2.0-preview4.20210.8.json");

                Assert.Equal(CatalogLeafType.PackageDelete, leaf.LeafType);
                Assert.IsType<PackageDeleteCatalogLeaf>(leaf);
            }

            public TheGetCatalogLeafAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }
        }

        public void Dispose()
        {
            RealHttpClientHandler.Dispose();
        }

        public CatalogClientTest(ITestOutputHelper output)
        {
            Output = output;
            RealHttpClientHandler = new HttpClientHandler();
            HttpMessageHandlerFactory = new TestHttpMessageHandlerFactory();
            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) => b(r, t);
            Settings = new NuGetInsightsSettings
            {
                V3ServiceIndex = "https://api.nuget.org/v3/index.json",
            };
            Options = new Mock<IOptions<NuGetInsightsSettings>>();
            Options.Setup(x => x.Value).Returns(() => Settings);
            HttpSource = new HttpSource(
                new PackageSource(Settings.V3ServiceIndex),
                () =>
                {
                    var httpMessageHandler = HttpMessageHandlerFactory.Create();
                    httpMessageHandler.InnerHandler = RealHttpClientHandler;
                    var resource = new HttpMessageHandlerResource(httpMessageHandler);
                    return Task.FromResult<HttpHandlerResource>(resource);
                },
                NullThrottle.Instance);
            ServiceIndexCache = new ServiceIndexCache(Options.Object);
            Target = new CatalogClient(
                HttpSource,
                ServiceIndexCache,
                Output.GetLogger<CatalogClient>());
        }

        public ITestOutputHelper Output { get; }
        public HttpClientHandler RealHttpClientHandler { get; }
        public TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public NuGetInsightsSettings Settings { get; }
        public Mock<IOptions<NuGetInsightsSettings>> Options { get; }
        public HttpSource HttpSource { get; }
        public ServiceIndexCache ServiceIndexCache { get; }
        public CatalogClient Target { get; }
    }
}
