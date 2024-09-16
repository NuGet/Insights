// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public delegate Task<HttpResponseMessage> GetResponseAsync(CancellationToken token);
    public delegate Task<HttpResponseMessage> SendMessageAsync(HttpRequestMessage request, CancellationToken token);
    public delegate Task<HttpResponseMessage?> SendMessageWithBaseAsync(HttpRequestMessage request, SendMessageAsync baseSendAsync, CancellationToken token);

    public class TestHttpMessageHandlerFactory : INuGetInsightsHttpMessageHandlerFactory
    {
        public SendMessageWithBaseAsync? OnSendAsync { get; set; }

        public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new ConcurrentQueue<HttpRequestMessage>();

        public ConcurrentQueue<(HttpRequestMessage OriginalRequest, HttpResponseMessage Response)> RequestAndResponses { get; } = new ConcurrentQueue<(HttpRequestMessage OriginalRequest, HttpResponseMessage Response)>();

        public IEnumerable<HttpResponseMessage> Responses => RequestAndResponses
            .Select(x => x.Response);

        public IEnumerable<HttpRequestMessage> SuccessRequests => Responses
            .Where(x => x.IsSuccessStatusCode && x.RequestMessage is not null)
            .Select(x => x.RequestMessage!);

        public void LogResponses(ITestOutputHelper output)
        {
            var logger = output.GetLogger<TestHttpMessageHandler>();
            logger.LogInformation("Responses captured by {ClassName}: {ResponseCount}x", nameof(TestHttpMessageHandlerFactory), RequestAndResponses.Count);
            foreach (var response in Responses)
            {
                logger.LogInformation(
                    "  - HTTP/{RequestVersion} {Method} {Url} -> HTTP/{ResponseVersion} {StatusCode} {ReasonPhrase}",
                    response.RequestMessage?.Version,
                    response.RequestMessage?.Method,
                    response.RequestMessage?.RequestUri?.AbsoluteUri,
                    response.Version,
                    (int)response.StatusCode,
                    response.ReasonPhrase);
            }
        }

        public void Clear()
        {
            Requests.Clear();
            RequestAndResponses.Clear();
        }

        public DelegatingHandler Create()
        {
            return new TestHttpMessageHandler(async (req, baseSendAsync, token) =>
            {
                var onSendAsync = OnSendAsync;
                if (onSendAsync is not null)
                {
                    return await onSendAsync(req, baseSendAsync, token);
                }

                return null;
            }, Requests, RequestAndResponses);
        }
    }
}
