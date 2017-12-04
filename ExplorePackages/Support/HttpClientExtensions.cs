using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Support
{
    public static class HttpClientExtensions
    {
        public static async Task<BlobMetadata> GetBlobMetadataAsync(this HttpClient httpClient, string url, ILogger log)
        {
            // Try to get all of the information using a HEAD request.
            using (var request = new HttpRequestMessage(HttpMethod.Head, url))
            {
                var stopwatch = Stopwatch.StartNew();
                using (var response = await httpClient.SendAsync(request))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return new BlobMetadata(
                            exists: false,
                            hasContentMD5Header: false,
                            contentMD5: null);
                    }

                    response.EnsureSuccessStatusCode();

                    var headerMD5Bytes = response.Content.Headers.ContentMD5;
                    if (headerMD5Bytes != null)
                    {
                        var contentMD5 = BytesToHex(headerMD5Bytes);
                        return new BlobMetadata(
                            exists: true,
                            hasContentMD5Header: true,
                            contentMD5: contentMD5);
                    }
                }
            }

            // If no Content-MD5 header was found in the response, calculate the package hash by downloading the
            // package.
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                var stopwatch = Stopwatch.StartNew();
                using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var buffer = new byte[16 * 1024];
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var md5 = new MD5IncrementalHash())
                    {
                        int read;
                        do
                        {
                            read = await stream.ReadAsync(buffer, 0, buffer.Length);
                            md5.AppendData(buffer, 0, read);
                        }
                        while (read > 0);

                        var hash = md5.GetHashAndReset();
                        var contentMD5 = BytesToHex(hash);
                        return new BlobMetadata(
                            exists: true,
                            hasContentMD5Header: false,
                            contentMD5: contentMD5);
                    }
                }
            }
        }

        private static string BytesToHex(byte[] hash)
        {
            return BitConverter
                .ToString(hash)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
