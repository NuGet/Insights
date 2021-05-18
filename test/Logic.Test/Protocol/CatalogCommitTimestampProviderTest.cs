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
    public class CatalogCommitTimestampProviderTest : BaseLogicIntegrationTest
    {
        public CatalogCommitTimestampProviderTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public CatalogCommitTimestampProvider Target => Host.Services.GetRequiredService<CatalogCommitTimestampProvider>();

        [Fact]
        public async Task ReturnsExpectedFirstTimestamps()
        {
            // Arrange
            var expected = new[]
            {
                "2015-02-01T06:22:45.8488496Z",
                "2015-02-01T06:22:57.0992608Z",
                "2015-02-01T06:23:09.6621617Z",
                "2015-02-01T06:23:19.6352720Z",
                "2015-02-01T06:23:32.5066430Z",
            }.Select(x => (DateTimeOffset?)DateTimeOffset.Parse(x)).ToList();

            // Act
            DateTimeOffset? min = DateTimeOffset.MinValue;
            var timestamps = new List<DateTimeOffset?>();
            for (var i = 0; i < 5; i++)
            {
                min = await Target.GetNextAsync(min.Value);
                timestamps.Add(min);
            }

            // Assert
            Assert.Equal(expected, timestamps);
            Assert.Equal(2, HttpMessageHandlerFactory.Requests.Count);
            Assert.Equal(1, HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsoluteUri == "https://api.nuget.org/v3/catalog0/index.json"));
            Assert.Equal(1, HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsoluteUri == "https://api.nuget.org/v3/catalog0/page0.json"));
        }

        [Fact]
        public async Task UsesTickPrecision()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(DateTimeOffset.Parse("2015-02-01T06:22:57.0992608Z").AddTicks(-1));

            // Assert
            Assert.Equal(DateTimeOffset.Parse("2015-02-01T06:22:57.0992608Z"), min);
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
            Assert.Single(HttpMessageHandlerFactory.Requests);
            Assert.Equal(1, HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsoluteUri == "https://api.nuget.org/v3/catalog0/page1.json"));
        }

        [Fact]
        public async Task ClearsCacheWhenIndexMaxIsTooLow()
        {
            // Arrange
            await Target.GetNextAsync(DateTimeOffset.Parse("2015-02-01T06:22:45.8488496Z"));
            HttpMessageHandlerFactory.Requests.Clear();

            // Act
            var min = await Target.GetNextAsync(DateTimeOffset.MaxValue);

            // Assert
            Assert.Null(min);
            Assert.Single(HttpMessageHandlerFactory.Requests);
            Assert.Equal(1, HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsoluteUri == "https://api.nuget.org/v3/catalog0/index.json"));
        }
    }
}
