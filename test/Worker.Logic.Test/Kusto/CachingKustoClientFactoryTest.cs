// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker;

namespace NuGet.Insights.Kusto
{
    public class CachingKustoClientFactoryTest
    {
        [Fact]
        public async Task GetKustoConnectionStringBuilderAsync_SetsDataSource()
        {
            // Act
            var builder = await CachingKustoClientFactory.GetKustoConnectionStringBuilderAsync(
                addIngest: false,
                Settings,
                LoggerFactory);

            // Assert
            Assert.Equal("https://mytestcluster.kusto.windows.net", builder.DataSource);
        }

        [Fact]
        public async Task GetKustoConnectionStringBuilderAsync_Ingest()
        {
            // Act
            var builder = await CachingKustoClientFactory.GetKustoConnectionStringBuilderAsync(
                addIngest: true,
                Settings,
                LoggerFactory);

            // Assert
            Assert.Equal("https://ingest-mytestcluster.kusto.windows.net", builder.DataSource);
        }

        [Fact]
        public async Task GetKustoConnectionStringBuilderAsync_ValidTokenProvider()
        {
            // Act
            var builder = await CachingKustoClientFactory.GetKustoConnectionStringBuilderAsync(
                addIngest: false,
                Settings,
                LoggerFactory);

            // Assert
            Assert.NotNull(builder.KustoTokenCredentialsProvider);
        }

        public ITestOutputHelper Output { get; }
        public Mock<IOptions<NuGetInsightsWorkerSettings>> Options { get; }
        public NuGetInsightsWorkerSettings Settings { get; }
        public ILoggerFactory LoggerFactory { get; }
        public CachingKustoClientFactory Target { get; }

        public CachingKustoClientFactoryTest(ITestOutputHelper output)
        {
            Output = output;
            Options = new Mock<IOptions<NuGetInsightsWorkerSettings>>();
            Settings = new NuGetInsightsWorkerSettings
            {
                KustoConnectionString = "https://mytestcluster.kusto.windows.net",
            };
            LoggerFactory = Output.GetLoggerFactory();

            Options.Setup(x => x.Value).Returns(new NuGetInsightsWorkerSettings());

            Target = new CachingKustoClientFactory(Options.Object, LoggerFactory);
        }
    }
}
