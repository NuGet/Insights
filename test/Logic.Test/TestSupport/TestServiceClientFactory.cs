// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Core.Pipeline;

namespace NuGet.Insights
{
    public class TestServiceClientFactory : ServiceClientFactory
    {
        public TestServiceClientFactory(
            Func<LoggingHttpHandler> loggingHandlerFactory,
            HttpClientHandler httpClientHandler,
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
            ILoggerFactory loggerFactory) : base(options, telemetryClient, loggerFactory)
        {
            LoggingHandlerFactory = loggingHandlerFactory;
            HttpClientHandler = httpClientHandler;
            HandlerFactory = new TestHttpMessageHandlerFactory(loggerFactory);
        }

        public Func<LoggingHttpHandler> LoggingHandlerFactory { get; }
        public HttpClientHandler HttpClientHandler { get; }
        public TestHttpMessageHandlerFactory HandlerFactory { get; }

        protected override HttpPipelineTransport GetHttpPipelineTransport()
        {
            var testHandler = HandlerFactory.Create();
            testHandler.InnerHandler = HttpClientHandler;

            var loggingHandler = LoggingHandlerFactory();
            loggingHandler.InnerHandler = testHandler;

            return new HttpClientTransport(loggingHandler);
        }
    }
}
