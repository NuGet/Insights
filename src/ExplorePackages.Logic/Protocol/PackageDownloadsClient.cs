using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class PackageDownloadsClient : IPackageDownloadsClient
    {
        private readonly HttpClient _httpClient;
        private readonly IThrottle _throttle;
        private readonly DownloadsV1JsonDeserializer _deserializer;
        private readonly IOptions<ExplorePackagesSettings> _options;

        public PackageDownloadsClient(
            HttpClient httpClient,
            IThrottle throttle,
            DownloadsV1JsonDeserializer deserializer,
            IOptions<ExplorePackagesSettings> options)
        {
            _httpClient = httpClient;
            _throttle = throttle;
            _deserializer = deserializer;
            _options = options;
        }

        public async Task<PackageDownloadSet> GetPackageDownloadSetAsync(string etag)
        {
            if (_options.Value.DownloadsV1Url == null)
            {
                throw new InvalidOperationException("The downloads.v1.json URL is required.");
            }

            var disposables = new Stack<IDisposable>();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _options.Value.DownloadsV1Url);
                disposables.Push(request);

                // Prior to this version, Azure Blob Storage did not put quotes around etag headers...
                request.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

                if (etag != null)
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", etag);
                }

                await _throttle.WaitAsync();
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var asOfTimestamp = response.Content.Headers.LastModified.Value.ToUniversalTime();
                disposables.Push(response);

                string newEtag;
                TextReader textReader;
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    newEtag = response.Headers.ETag.ToString();

                    var stream = await response.Content.ReadAsStreamAsync();
                    disposables.Push(stream);

                    textReader = new StreamReader(stream);
                    disposables.Push(textReader);
                }
                else if (etag != null && response.StatusCode == HttpStatusCode.NotModified)
                {
                    newEtag = etag;

                    textReader = new StringReader("[]");
                    disposables.Push(textReader);
                }
                else
                {
                    response.Dispose();
                    throw new HttpRequestException($"Response status code is not 200 OK: {((int)response.StatusCode)} ({response.ReasonPhrase})");
                }

                return new PackageDownloadSet(
                    asOfTimestamp,
                    _options.Value.DownloadsV1Url,
                    newEtag,
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
