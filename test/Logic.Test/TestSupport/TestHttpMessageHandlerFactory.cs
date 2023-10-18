// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public delegate Task<HttpResponseMessage> GetResponseAsync(CancellationToken token);
    public delegate Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage request, CancellationToken token);
    public delegate Task<HttpResponseMessage> SendMessageWithBaseAsync(HttpRequestMessage request, SendMessageAsync baseSendAsync, CancellationToken token);

    public class TestHttpMessageHandlerFactory : INuGetInsightsHttpMessageHandlerFactory
    {
        public SendMessageWithBaseAsync OnSendAsync { get; set; }

        private ConcurrentQueue<HttpRequestMessage> Requests { get; } = new ConcurrentQueue<HttpRequestMessage>();

        public ConcurrentQueue<HttpResponseMessage> Responses { get; } = new ConcurrentQueue<HttpResponseMessage>();

        public IEnumerable<HttpRequestMessage> SuccessRequests => Responses
            .Where(x => x.IsSuccessStatusCode && x.RequestMessage is not null)
            .Select(x => x.RequestMessage);

        public void LogResponses(ITestOutputHelper output)
        {
            var logger = output.GetLogger<TestHttpMessageHandler>();
            logger.LogInformation("Responses captured by {ClassName}: {ResponseCount}x", nameof(TestHttpMessageHandlerFactory), Responses.Count);
            foreach (var response in Responses)
            {
                logger.LogInformation(
                    "  - HTTP/{RequestVersion} {Method} {Url} -> HTTP/{ResponseVersion} {StatusCode} {ReasonPhrase}",
                    response.RequestMessage?.Version,
                    response.RequestMessage?.Method,
                    response.RequestMessage?.RequestUri.AbsoluteUri,
                    response.Version,
                    (int)response.StatusCode,
                    response.ReasonPhrase);
            }
        }

        public void Clear()
        {
            Requests.Clear();
            Responses.Clear();
        }

        public DelegatingHandler Create()
        {
            return new TestHttpMessageHandler(async (req, baseSendAsync, token) =>
            {
                if (OnSendAsync != null)
                {
                    return await OnSendAsync(req, baseSendAsync, token);
                }

                return null;
            }, Requests, Responses);
        }
    }
}
