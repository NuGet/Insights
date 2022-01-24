// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class VerifiedPackagesClient
    {
        private readonly HttpClient _httpClient;
        private readonly IThrottle _throttle;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public VerifiedPackagesClient(
            HttpClient httpClient,
            IThrottle throttle,
            IOptions<NuGetInsightsSettings> options)
        {
            _httpClient = httpClient;
            _throttle = throttle;
            _options = options;
        }

        public async Task<AsOfData<VerifiedPackage>> GetAsync()
        {
            if (_options.Value.VerifiedPackagesV1Url == null)
            {
                throw new InvalidOperationException("The verifiedPackages.json URL is required.");
            }

            HttpResponseMessage response = null;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _options.Value.VerifiedPackagesV1Url);

                request.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

                await _throttle.WaitAsync();
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                var asOfTimestamp = response.Content.Headers.LastModified.Value.ToUniversalTime();
                var etag = response.Headers.ETag.ToString();
                var stream = await response.Content.ReadAsStreamAsync();

                return new AsOfData<VerifiedPackage>(
                    asOfTimestamp,
                    _options.Value.VerifiedPackagesV1Url,
                    etag,
                    AsyncEnumerableEx.Using(
                        () => new ResponseAndThrottle(response, _throttle),
                        _ => JsonSerializer
                            .DeserializeAsyncEnumerable<string>(stream)
                            .Select(x => new VerifiedPackage(x))));
            }
            catch
            {
                response?.Dispose();
                _throttle.Release();
                throw;
            }
        }
    }
}
