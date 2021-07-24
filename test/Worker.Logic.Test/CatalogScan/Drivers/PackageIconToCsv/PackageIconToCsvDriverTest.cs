// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    public class PackageIconToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageIconToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<PackageIcon> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageIcon>>();

        [Fact]
        public async Task AnimatedGif_Opaque()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.19.11.35.46/fastmicroservices.marconi.web.0.1.1.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "FastMicroservices.Marconi.Web",
                PackageVersion = "0.1.1",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Gif", record.Format);
            Assert.Equal(35, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("9b29ae9edd938a2bf2d3031fcc2e2dd82e6d6e7d2fe551891a9ed5f179d4adf6", record.Signature);
            Assert.Equal("kWloGw9VYuRnd1iwV0y+D++cu5uGrCz7srX89eWpHtU=", record.SHA256);
            Assert.Equal(140259, record.FileSize);
            Assert.Equal(761, record.Width);
            Assert.Equal(371, record.Height);
        }

        [Fact]
        public async Task NonAnimatedGif_Transparent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.25.11.09.46/google.adwords.examples.vb.21.1.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Google.AdWords.Examples.VB",
                PackageVersion = "21.1.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Gif", record.Format);
            Assert.Equal(1, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("cd6cb7cfac142e1643d6f084c35482b755904a88816c6081b525a67c27b26779", record.Signature);
            Assert.Equal("BMhjIEQ2iswW89LHeMdX15mNCtpkFbKM/JODKQJeMC4=", record.SHA256);
            Assert.Equal(1654, record.FileSize);
            Assert.Equal(48, record.Width);
            Assert.Equal(48, record.Height);
        }

        [Fact]
        public async Task Png_Transparent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.18.21.30.27/accord.controls.audio.3.6.2-alpha.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Accord.Controls.Audio",
                PackageVersion = "3.6.2-alpha",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Png", record.Format);
            Assert.Equal(1, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("d1dca128fe19ffedc8b259f58c39f731a98ad52304376ef6d4ab233e5f928b98", record.Signature);
            Assert.Equal("oNhVoCSr6BKWb3RmqLn7NXPEjfhijVGLjNVmIpRBkCI=", record.SHA256);
            Assert.Equal(9327, record.FileSize);
            Assert.Equal(100, record.Width);
            Assert.Equal(100, record.Height);
        }

        [Fact]
        public async Task Svg()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.07.09.09.39.04/antdesign.checkbox.0.0.15-alpha.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "AntDesign.Checkbox",
                PackageVersion = "0.0.15-alpha",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("image/svg+xml", record.ContentType);
            Assert.Equal("Svg", record.Format);
            Assert.Equal(1, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("c519e64ccc405295fa61a583ff4959eea6842b72a05cf2cc2ca9e9e1c0019c8b", record.Signature);
            Assert.Equal("craAEC3eL5Oa3Qp1t+SU4dNwPG4YF8POjnrljAqg8cE=", record.SHA256);
            Assert.Equal(4729, record.FileSize);
            Assert.Equal(200, record.Width);
            Assert.Equal(200, record.Height);
        }

        [Fact]
        public async Task Jpg()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.01.22.07.33/dk.android.floatingactionbutton.1.2.0.300.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "DK.Android.FloatingActionButton",
                PackageVersion = "1.2.0.300",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Jpeg", record.Format);
            Assert.Equal(1, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("ecc6f86ea416d83ad54d038178fe670a24d394c57d5bc4032180b6991b974002", record.Signature);
            Assert.Equal("sbJhsY1+2RtK27sbEKC9N3upB6U3TeoJp1fimRQ4HV8=", record.SHA256);
            Assert.Equal(5380, record.FileSize);
            Assert.Equal(145, record.Width);
            Assert.Equal(145, record.Height);
        }

        [Fact]
        public async Task IconWithSingleResolution_Transparent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.19.19.58.46/nettle.data.1.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Nettle.Data",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Ico", record.Format);
            Assert.Equal(1, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("7bf8efaa9c1a0de027656df9b2516200a896403a7ae91b07b2061f8cd84b5206", record.Signature);
            Assert.Equal("d/CXoPpvQF2j4FgcQgxjHbgWz55vZ6lfy3bh+GT3qYg=", record.SHA256);
            Assert.Equal(9662, record.FileSize);
            Assert.Equal(48, record.Width);
            Assert.Equal(48, record.Height);
        }

        [Fact]
        public async Task IconWithMultipleResolutions_Transparent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.03.29.20.30.00/microsoft.windows.cppwinrt.2.0.210329.4.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.Windows.CppWinRT",
                PackageVersion = "2.0.210329.4",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Ico", record.Format);
            Assert.Equal(4, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("9ffe66c5ffa25df6caf1deeca7d9e4a1c7ce6f539b7fb69e38589f8fe4a12140", record.Signature);
            Assert.Equal("clvBdjr3NPKy7fKcPQYpNQLtOcl9MeTML0x0mXkcJl4=", record.SHA256);
            Assert.Equal(90022, record.FileSize);
            Assert.Equal(128, record.Width);
            Assert.Equal(128, record.Height);
        }
    }
}
