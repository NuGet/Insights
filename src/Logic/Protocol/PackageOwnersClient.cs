using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class PackageOwnersClient
    {
        private readonly HttpClient _httpClient;
        private readonly IThrottle _throttle;
        private readonly OwnersV2JsonDeserializer _deserializer;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public PackageOwnersClient(
            HttpClient httpClient,
            IThrottle throttle,
            OwnersV2JsonDeserializer deserializer,
            IOptions<NuGetInsightsSettings> options)
        {
            _httpClient = httpClient;
            _throttle = throttle;
            _deserializer = deserializer;
            _options = options;
        }

        public async Task<PackageOwnerSet> GetPackageOwnerSetAsync()
        {
            if (_options.Value.OwnersV2Url == null)
            {
                throw new InvalidOperationException("The owners.v2.json URL is required.");
            }

            var disposables = new Stack<IDisposable>();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _options.Value.OwnersV2Url);
                disposables.Push(request);

                request.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

                await _throttle.WaitAsync();
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                disposables.Push(response);

                response.EnsureSuccessStatusCode();

                var asOfTimestamp = response.Content.Headers.LastModified.Value.ToUniversalTime();
                var etag = response.Headers.ETag.ToString();

                var stream = await response.Content.ReadAsStreamAsync();
                disposables.Push(stream);

                var textReader = new StreamReader(stream);
                disposables.Push(textReader);

                return new PackageOwnerSet(
                    asOfTimestamp,
                    _options.Value.OwnersV2Url,
                    etag,
                    _deserializer.DeserializeAsync(textReader, disposables, _throttle));
            }
            catch
            {
                _throttle.Release();

                while (disposables.Any())
                {
                    disposables.Pop()?.Dispose();
                }

                throw;
            }
        }
    }
}
