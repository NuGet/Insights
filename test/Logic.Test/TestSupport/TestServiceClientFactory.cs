// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class TestServiceClientFactory : ServiceClientFactory
    {
        public TestServiceClientFactory(
            Func<LoggingHandler> loggingHandlerFactory,
            HttpClientHandler httpClientHandler,
            IOptions<NuGetInsightsSettings> options,
            ILoggerFactory logger) : base(options, logger)
        {
            LoggingHandlerFactory = loggingHandlerFactory;
            HttpClientHandler = httpClientHandler;
        }

        public Func<LoggingHandler> LoggingHandlerFactory { get; }
        public HttpClientHandler HttpClientHandler { get; }
        public TestHttpMessageHandlerFactory HandlerFactory { get; } = new TestHttpMessageHandlerFactory();

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
