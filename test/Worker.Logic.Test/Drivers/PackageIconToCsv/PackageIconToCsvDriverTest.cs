// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        public async Task SerializesFrameDimensionsInLexOrder()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2023.05.04.04.00.50/microsoft.extensions.primitives.2.2.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.Extensions.Primitives",
                PackageVersion = "2.2.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal("[{\"Height\":512,\"Width\":512}]", record.FrameDimensions);
        }

        [Fact]
        public async Task Gif_Animated_Opaque()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.19.11.35.46/fastmicroservices.marconi.web.0.1.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "FastMicroservices.Marconi.Web",
                PackageVersion = "0.1.1",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Gif", record.HeaderFormat);
            Assert.Equal(35, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("mymunt2Tiivy0wMfzC4t2C5tbn0v5VGJGp7V8XnUrfY=", record.Signature);
            Assert.Equal("kWloGw9VYuRnd1iwV0y+D++cu5uGrCz7srX89eWpHtU=", record.SHA256);
            Assert.Equal(140259, record.FileSize);
            Assert.Equal(761, record.Width);
            Assert.Equal(371, record.Height);
        }

        [Fact]
        public async Task Gif_Animated_Transparent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.29.06.21.31/csharpgl.1.0.7.4.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "CSharpGL",
                PackageVersion = "1.0.7.4",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Gif", record.HeaderFormat);
            Assert.Equal(60, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("Dc7NJQutbaEPtm/8+1h4a4zP1r4H3eZY0G0Ut1kn2TE=", record.Signature);
            Assert.Equal("kPL0cxjYIYYgU3pLmcX8S8DY9mvH9sZcdYBd/8Z6YAA=", record.SHA256);
            Assert.Equal(128414, record.FileSize);
            Assert.Equal(64, record.Width);
            Assert.Equal(64, record.Height);
        }

        [Fact]
        public async Task Gif_NonAnimated_Transparent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.25.11.09.46/google.adwords.examples.vb.21.1.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Google.AdWords.Examples.VB",
                PackageVersion = "21.1.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Gif", record.HeaderFormat);
            Assert.Equal(1, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("zWy3z6wULhZD1vCEw1SCt1WQSoiBbGCBtSWmfCeyZ3k=", record.Signature);
            Assert.Equal("BMhjIEQ2iswW89LHeMdX15mNCtpkFbKM/JODKQJeMC4=", record.SHA256);
            Assert.Equal(1654, record.FileSize);
            Assert.Equal(48, record.Width);
            Assert.Equal(48, record.Height);
        }

        [Fact]
        public async Task Gif_NonAnimated_Opaque()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.14.18.07.34/czmq.2.1.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "czmq",
                PackageVersion = "2.1.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Gif", record.HeaderFormat);
            Assert.Equal(1, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("xo2jhcWYyy5Qt4qSOG1JODp9dyww/eeIoKqr4oj8Nj8=", record.Signature);
            Assert.Equal("0nGKT4boTSLkVV0G5q/Ye4buyutUqQe/yWRjlZgQVTM=", record.SHA256);
            Assert.Equal(8600, record.FileSize);
            Assert.Equal(381, record.Width);
            Assert.Equal(119, record.Height);
        }

        [Fact]
        public async Task Png_Transparent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.18.21.30.27/accord.controls.audio.3.6.2-alpha.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Accord.Controls.Audio",
                PackageVersion = "3.6.2-alpha",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Png", record.HeaderFormat);
            Assert.Equal(1, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("0dyhKP4Z/+3Isln1jDn3MamK1SMEN2721KsjPl+Si5g=", record.Signature);
            Assert.Equal("oNhVoCSr6BKWb3RmqLn7NXPEjfhijVGLjNVmIpRBkCI=", record.SHA256);
            Assert.Equal(9327, record.FileSize);
            Assert.Equal(100, record.Width);
            Assert.Equal(100, record.Height);
        }

        [Fact]
        public async Task Svg()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.07.09.09.39.04/antdesign.checkbox.0.0.15-alpha.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "AntDesign.Checkbox",
                PackageVersion = "0.0.15-alpha",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("image/svg+xml", record.ContentType);
            Assert.Equal("Svg", record.HeaderFormat);
            Assert.Equal(1, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("xRnmTMxAUpX6YaWD/0lZ7qaEK3KgXPLMLKnp4cABnIs=", record.Signature);
            Assert.Equal("craAEC3eL5Oa3Qp1t+SU4dNwPG4YF8POjnrljAqg8cE=", record.SHA256);
            Assert.Equal(4729, record.FileSize);
            Assert.Equal(200, record.Width);
            Assert.Equal(200, record.Height);
        }

        [Fact]
        public async Task Jpeg()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.01.22.07.33/dk.android.floatingactionbutton.1.2.0.300.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "DK.Android.FloatingActionButton",
                PackageVersion = "1.2.0.300",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Jpeg", record.HeaderFormat);
            Assert.Equal(1, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("7Mb4bqQW2DrVTQOBeP5nCiTTlMV9W8QDIYC2mRuXQAI=", record.Signature);
            Assert.Equal("sbJhsY1+2RtK27sbEKC9N3upB6U3TeoJp1fimRQ4HV8=", record.SHA256);
            Assert.Equal(5380, record.FileSize);
            Assert.Equal(145, record.Width);
            Assert.Equal(145, record.Height);
        }

        [Fact]
        public async Task Ico_SingleResolution_Transparent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.19.19.58.46/nettle.data.1.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Nettle.Data",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Ico", record.HeaderFormat);
            Assert.Equal(1, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("e/jvqpwaDeAnZW35slFiAKiWQDp66RsHsgYfjNhLUgY=", record.Signature);
            Assert.Equal("d/CXoPpvQF2j4FgcQgxjHbgWz55vZ6lfy3bh+GT3qYg=", record.SHA256);
            Assert.Equal(9662, record.FileSize);
            Assert.Equal(48, record.Width);
            Assert.Equal(48, record.Height);
        }

        [Fact]
        public async Task Ico_SingleResolution_Opaque()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.02.28.14.01.05/wl.1.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "WL",
                PackageVersion = "1.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Ico", record.HeaderFormat);
            Assert.Equal(1, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("S7rlSNSmIqiM5aiyzFhy3I1o5x5vzP0EJLk+o6UN1as=", record.Signature);
            Assert.Equal("hmPIhoiXa/mRU5cCBzPSIQpLB9eF7/PlsCyTnvlBsgk=", record.SHA256);
            Assert.Equal(894, record.FileSize);
            Assert.Equal(16, record.Width);
            Assert.Equal(16, record.Height);
        }

        [Fact]
        public async Task Ico_MultipleResolutions_Transparent()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.03.29.20.30.00/microsoft.windows.cppwinrt.2.0.210329.4.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.Windows.CppWinRT",
                PackageVersion = "2.0.210329.4",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Ico", record.HeaderFormat);
            Assert.Equal(4, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("n/5mxf+iXfbK8d7sp9nkocfOb1Obf7aeOFifj+ShIUA=", record.Signature);
            Assert.Equal("clvBdjr3NPKy7fKcPQYpNQLtOcl9MeTML0x0mXkcJl4=", record.SHA256);
            Assert.Equal(90022, record.FileSize);
            Assert.Equal(128, record.Width);
            Assert.Equal(128, record.Height);
            Assert.Equal("[{\"Height\":128,\"Width\":128},{\"Height\":64,\"Width\":64},{\"Height\":32,\"Width\":32},{\"Height\":16,\"Width\":16}]", record.FrameDimensions);
        }

        [Fact]
        public async Task Icon_MultipleResolutions_Opaque()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.13.15.31.08/a3d.0.0.2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "a3d",
                PackageVersion = "0.0.2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Ico", record.HeaderFormat);
            Assert.Equal(4, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("8uCsQLNuMAFq0k+zNb7OegMROQUi4xR71t86T/rzKtI=", record.Signature);
            Assert.Equal("annlRNcGe0pXJyQ19EqkB7vyNNqBrniNvlteLm8auBQ=", record.SHA256);
            Assert.Equal(18406, record.FileSize);
            Assert.Equal(64, record.Width);
            Assert.Equal(64, record.Height);
            Assert.Equal("[{\"Height\":64,\"Width\":64},{\"Height\":32,\"Width\":32},{\"Height\":16,\"Width\":16}]", record.FrameDimensions);
        }

        [Fact]
        public async Task Ico_MultipleFormats()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.10.28.09.55.04/eltra.1.2.3.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Eltra",
                PackageVersion = "1.2.3",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Ico", record.HeaderFormat);
            Assert.Equal(6, record.FrameCount);
            Assert.False(record.IsOpaque);
            Assert.Equal("WpsaubBaAFel81d8Oafx6Opo2dwOXqEUU7yEhWH3Lps=", record.Signature);
            Assert.Equal("4w8PIYgAD/mAKoVbwqoekm0n/tGDdGVnJOc8n9jAzMM=", record.SHA256);
            Assert.Equal(113018, record.FileSize);
            Assert.Equal(256, record.Width);
            Assert.Equal(256, record.Height);
            Assert.Equal("[{\"Height\":256,\"Width\":256},{\"Height\":128,\"Width\":128},{\"Height\":64,\"Width\":64},{\"Height\":48,\"Width\":48},{\"Height\":32,\"Width\":32},{\"Height\":16,\"Width\":16}]", record.FrameDimensions);
            Assert.Equal("[\"Png\",\"Ico\"]", record.FrameFormats);
        }

        [Fact]
        public async Task Gif87_Animated()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.19.08.38.23/xamarinshimmer.2.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "XamarinShimmer",
                PackageVersion = "2.0.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Gif87", record.HeaderFormat);
            Assert.Equal(17, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("AcRL7lyG+9mgAGGejv5/+C+oYdNv9DW/FUKJ9w5aCDI=", record.Signature);
            Assert.Equal("QIMN9LskFW7QdP/smay4fdXqXPhgXR1UToXxDMolXdo=", record.SHA256);
            Assert.Equal(60724, record.FileSize);
            Assert.Equal(800, record.Width);
            Assert.Equal(450, record.Height);
            Assert.Equal(
                "[" +
                "{\"Height\":450,\"Width\":800}," +
                "{\"Height\":440,\"Width\":792}," +
                "{\"Height\":428,\"Width\":795}," +
                "{\"Height\":429,\"Width\":796}," +
                "{\"Height\":428,\"Width\":777}," +
                "{\"Height\":403,\"Width\":773}," +
                "{\"Height\":50,\"Width\":246}," +
                "{\"Height\":49,\"Width\":185}," +
                "{\"Height\":36,\"Width\":104}," +
                "{\"Height\":36,\"Width\":51}," +
                "{\"Height\":1,\"Width\":1}" +
                "]",
                record.FrameDimensions);
        }

        [Fact]
        public async Task Gif87_NonAnimated()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.29.02.15.15/lua.5.3.4.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Lua",
                PackageVersion = "5.3.4",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageIconResultType.Available, record.ResultType);
            Assert.Equal("Gif87", record.HeaderFormat);
            Assert.Equal(1, record.FrameCount);
            Assert.True(record.IsOpaque);
            Assert.Equal("wB8/mfyiv2/84708AgnpL9ti7/NidfNUuR15veM4LDo=", record.Signature);
            Assert.Equal("nDoyCO8F4SEQ+be0kc72duZQ5Ovz02xEFCaGZrzJXYo=", record.SHA256);
            Assert.Equal(4232, record.FileSize);
            Assert.Equal(128, record.Width);
            Assert.Equal(128, record.Height);
        }
    }
}
