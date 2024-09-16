// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class HttpPipelineIntegrationTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task RetriesTimeout()
        {
            // Arrange
            ConfigureSettings = x => x.HttpClientNetworkTimeout = TimeSpan.FromSeconds(1);
            int requestCount = 0;
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                if (Interlocked.Increment(ref requestCount) == 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), t);
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = r, Content = new StringContent("done") };
            };

            var httpClient = Host.Services.GetRequiredService<Func<HttpClient>>()();

            // Act
            var response = await httpClient.GetAsync("https://api.nuget.org/v3/index.json");

            // Assert
            Assert.Equal(2, HttpMessageHandlerFactory.Requests.Count);
            TelemetryClient.Metrics.TryGetValue(new("RetryHttpMessageHandler.RetryException.SleepDurationSeconds", "ExceptionType", "DelaySource"), out var metric);
            Assert.NotNull(metric);
            var entry = Assert.Single(metric.MetricValues);
            Assert.Equal(Options.Value.HttpClientMaxRetryDelay.TotalSeconds, entry.MetricValue);
            Assert.Equal("Polly.Timeout.TimeoutRejectedException", entry.DimensionValues[0]);
            Assert.Equal("max retry delay, was exponential back-off", entry.DimensionValues[1]);
        }

        [Fact]
        public async Task RetriesWithExponentialBackOff()
        {
            // Arrange
            int requestCount = 0;
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                await Task.Yield();

                if (Interlocked.Increment(ref requestCount) == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = r, Content = new StringContent("done") };
            };

            var httpClient = Host.Services.GetRequiredService<Func<HttpClient>>()();

            // Act
            var response = await httpClient.GetAsync("https://api.nuget.org/v3/index.json");

            // Assert
            Assert.Equal(2, HttpMessageHandlerFactory.RequestAndResponses.Count);
            TelemetryClient.Metrics.TryGetValue(new("RetryHttpMessageHandler.RetryStatusCode.SleepDurationSeconds", "StatusCode", "DelaySource"), out var metric);
            Assert.NotNull(metric);
            var entry = Assert.Single(metric.MetricValues);
            Assert.Equal(Options.Value.HttpClientMaxRetryDelay.TotalSeconds, entry.MetricValue);
            Assert.Equal("500", entry.DimensionValues[0]);
            Assert.Equal("max retry delay, was exponential back-off", entry.DimensionValues[1]);
        }

        [Fact]
        public async Task RetriesWithRetryAfterDelta()
        {
            // Arrange
            int requestCount = 0;
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                await Task.Yield();

                if (Interlocked.Increment(ref requestCount) == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    {
                        Headers =
                        {
                            RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1)),
                        },
                        RequestMessage = r,
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = r, Content = new StringContent("done") };
            };

            var httpClient = Host.Services.GetRequiredService<Func<HttpClient>>()();

            // Act
            var response = await httpClient.GetAsync("https://api.nuget.org/v3/index.json");

            // Assert
            Assert.Equal(2, HttpMessageHandlerFactory.RequestAndResponses.Count);
            TelemetryClient.Metrics.TryGetValue(new("RetryHttpMessageHandler.RetryStatusCode.SleepDurationSeconds", "StatusCode", "DelaySource"), out var metric);
            Assert.NotNull(metric);
            var entry = Assert.Single(metric.MetricValues);
            Assert.Equal(Options.Value.HttpClientMaxRetryDelay.TotalSeconds, entry.MetricValue);
            Assert.Equal("429", entry.DimensionValues[0]);
            Assert.Equal("max retry delay, was Retry-After delta", entry.DimensionValues[1]);
        }

        public HttpPipelineIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
