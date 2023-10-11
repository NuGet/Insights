// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class CatalogCommitTimestampProviderIntegrationTest : BaseLogicIntegrationTest
    {
        private const string IndexUrl = "https://api.nuget.org/v3/catalog0/index.json";
        private const string Page0Url = "https://api.nuget.org/v3/catalog0/page0.json";
        private const string Page1Url = "https://api.nuget.org/v3/catalog0/page1.json";
        private const string Page2Url = "https://api.nuget.org/v3/catalog0/page2.json";

        private static readonly IReadOnlyList<DateTimeOffset?> CommitTimestamps = new[]
        {
            "2015-02-01T06:22:45.8488496Z", // Page 0 min
            "2015-02-01T06:22:57.0992608Z",
            "2015-02-01T06:23:09.6621617Z",
            "2015-02-01T06:23:19.6352720Z",
            "2015-02-01T06:23:32.5066430Z",
            "2015-02-01T06:23:47.1477371Z",
            "2015-02-01T06:24:00.6168490Z",
            "2015-02-01T06:24:15.8202028Z",
            "2015-02-01T06:24:32.3831288Z",
            "2015-02-01T06:24:49.3523119Z",
            "2015-02-01T06:25:03.0869910Z",
            "2015-02-01T06:25:23.6339473Z",
            "2015-02-01T06:25:39.0248056Z",
            "2015-02-01T06:25:55.3532442Z",
            "2015-02-01T06:26:10.6505767Z",
            "2015-02-01T06:26:26.2598201Z",
            "2015-02-01T06:26:42.9943741Z",
            "2015-02-01T06:27:00.9014909Z",
            "2015-02-01T06:27:22.4637986Z",
            "2015-02-01T06:27:41.0890376Z",
            "2015-02-01T06:27:59.3862616Z",
            "2015-02-01T06:28:18.0432918Z",
            "2015-02-01T06:28:47.3090561Z",
            "2015-02-01T06:29:06.1687927Z",
            "2015-02-01T06:29:25.9347981Z",
            "2015-02-01T06:29:46.5914446Z",
            "2015-02-01T06:30:11.7477681Z", // Page 0 max
            "2015-02-01T06:30:42.5921094Z", // Page 1 min
            "2015-02-01T06:30:59.3736815Z",
            "2015-02-01T06:31:13.3739253Z",
            "2015-02-01T06:31:29.8741387Z",
            "2015-02-01T06:31:50.2494541Z",
            "2015-02-01T06:32:12.4686304Z",
            "2015-02-01T06:32:21.7966272Z",
            "2015-02-01T06:32:43.2497722Z",
            "2015-02-01T06:33:04.5000408Z",
            "2015-02-01T06:33:20.5002469Z",
            "2015-02-01T06:33:37.0317089Z",
            "2015-02-01T06:33:50.7037553Z",
            "2015-02-01T06:34:14.7506740Z",
            "2015-02-01T06:34:49.0953144Z",
            "2015-02-01T06:35:13.6104534Z",
            "2015-02-01T06:35:37.9231903Z",
            "2015-02-01T06:36:01.8609221Z",
            "2015-02-01T06:36:22.5329035Z",
            "2015-02-01T06:36:44.2674663Z",
            "2015-02-01T06:37:00.5956962Z",
            "2015-02-01T06:37:39.0487308Z",
            "2015-02-01T06:38:02.3922505Z",
            "2015-02-01T06:38:32.1898934Z",
            "2015-02-01T06:38:55.3147979Z",
            "2015-02-01T06:39:12.3458220Z",
            "2015-02-01T06:39:31.1428178Z",
            "2015-02-01T06:39:53.9553899Z", // Page 1 max
            "2015-02-01T06:40:20.9397654Z", // Page 2 min
            "2015-02-01T06:40:32.4085154Z",
            "2015-02-01T06:40:46.4085037Z",

        }.Select(x => (DateTimeOffset?)DateTimeOffset.Parse(x)).ToList();

        public CatalogCommitTimestampProviderIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public CatalogCommitTimestampProvider Target => Host.Services.GetRequiredService<CatalogCommitTimestampProvider>();

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ReturnsExpectedNextTimestamps_Backward(int offsetTicks)
        {
            // Act
            var timestamps = new List<DateTimeOffset?>();
            for (var i = CommitTimestamps.Count - 1; i >= 0; i--)
            {
                var next = await Target.GetNextAsync(CommitTimestamps[i].Value.AddTicks(-2 + offsetTicks));
                timestamps.Insert(0, next);
            }

            // Assert
            Assert.Equal(CommitTimestamps.Count, timestamps.Count);
            Assert.Equal(CommitTimestamps, timestamps);
            Assert.Equal(4, HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.Host == "api.nuget.org" && x.IsSuccessStatusCode));
            Assert.NotEmpty(HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsoluteUri == IndexUrl));
            Assert.NotEmpty(HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsoluteUri == Page0Url));
            Assert.NotEmpty(HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsoluteUri == Page1Url));
            Assert.NotEmpty(HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsoluteUri == Page2Url));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ReturnsExpectedNextTimestamps_Forward(int offsetTicks)
        {
            // Act
            DateTimeOffset? min = DateTimeOffset.MinValue;
            var timestamps = new List<DateTimeOffset?>();
            for (var i = 0; i < CommitTimestamps.Count; i++)
            {
                min = await Target.GetNextAsync(min.Value.AddTicks(offsetTicks));
                timestamps.Add(min);
            }

            // Assert
            Assert.Equal(CommitTimestamps.Count, timestamps.Count);
            Assert.Equal(CommitTimestamps, timestamps);
            Assert.Equal(4, HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.Host == "api.nuget.org" && x.IsSuccessStatusCode));
            Assert.NotEmpty(HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsoluteUri == IndexUrl));
            Assert.NotEmpty(HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsoluteUri == Page0Url));
            Assert.NotEmpty(HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsoluteUri == Page1Url));
            Assert.NotEmpty(HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsoluteUri == Page2Url));
        }

        [Fact]
        public async Task UsesTickPrecision_LessThan_InsidePage()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(DateTimeOffset.Parse("2015-02-01T06:22:57.0992608Z").AddTicks(-1));

            // Assert
            Assert.Equal(DateTimeOffset.Parse("2015-02-01T06:22:57.0992608Z"), min);
        }

        [Fact]
        public async Task UsesTickPrecision_Equals_InsidePage()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(DateTimeOffset.Parse("2015-02-01T06:22:57.0992608Z"));

            // Assert
            Assert.Equal(DateTimeOffset.Parse("2015-02-01T06:23:09.6621617Z"), min);
        }

        [Fact]
        public async Task UsesTickPrecision_GreaterThan_InsidePage()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(DateTimeOffset.Parse("2015-02-01T06:22:57.0992608Z").AddTicks(1));

            // Assert
            Assert.Equal(DateTimeOffset.Parse("2015-02-01T06:23:09.6621617Z"), min);
        }

        [Fact]
        public async Task UsesTickPrecision_LessThan_PageBound()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(DateTimeOffset.Parse("2015-02-01T06:30:11.7477681Z").AddTicks(-1));

            // Assert
            Assert.Equal(DateTimeOffset.Parse("2015-02-01T06:30:11.7477681Z"), min);
        }

        [Fact]
        public async Task UsesTickPrecision_Equals_PageBound()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(DateTimeOffset.Parse("2015-02-01T06:30:11.7477681Z"));

            // Assert
            Assert.Equal(DateTimeOffset.Parse("2015-02-01T06:30:42.5921094Z"), min);
        }

        [Fact]
        public async Task UsesTickPrecision_GreaterThan_PageBound()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(DateTimeOffset.Parse("2015-02-01T06:30:11.7477681Z").AddTicks(1));

            // Assert
            Assert.Equal(DateTimeOffset.Parse("2015-02-01T06:30:42.5921094Z"), min);
        }

        [Fact]
        public async Task ReadsNextPage()
        {
            // Arrange
            await Target.GetNextAsync(DateTimeOffset.MinValue);
            HttpMessageHandlerFactory.Requests.Clear();

            // Act
            var min = await Target.GetNextAsync(DateTimeOffset.Parse("2015-02-01T06:30:11.7477681Z"));

            // Assert
            Assert.Equal(DateTimeOffset.Parse("2015-02-01T06:30:42.5921094Z"), min);
            Assert.Equal(1, HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.RequestUri.AbsoluteUri == Page1Url && x.IsSuccessStatusCode));
            Assert.NotEmpty(HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsoluteUri == Page1Url));
        }
    }
}
