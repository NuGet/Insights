// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class RedirectResolver
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RedirectResolver> _logger;

        public RedirectResolver(
            HttpClient httpClient,
            ILogger<RedirectResolver> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<Uri> FollowRedirectsAsync(string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            var lastUrl = response.RequestMessage?.RequestUri;
            if (lastUrl is null)
            {
                throw new InvalidOperationException("No request URL was found on the response message.");
            }

            if (lastUrl.OriginalString == url)
            {
                _logger.LogInformation(
                    "The request HEAD {Url} does not have a redirect. The final status is {StatusCode} {ReasonPhrase}.",
                    url,
                    (int)response.StatusCode,
                    response.ReasonPhrase);
            }
            else
            {
                _logger.LogInformation(
                    "The request HEAD {FirstUrl} has a redirect. The final redirect is to {LastUrl} with a status of {StatusCode} {ReasonPhrase}.",
                    url,
                    lastUrl.AbsoluteUri,
                    (int)response.StatusCode,
                    response.ReasonPhrase);
            }

            return lastUrl;
        }
    }
}

