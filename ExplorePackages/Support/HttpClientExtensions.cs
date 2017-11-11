using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Support
{
    public static class HttpClientExtensions
    {
        public static async Task<string> GetContentMd5Async(this HttpClient httpClient, string url)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Head, url))
            using (var response = await httpClient.SendAsync(request))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var hash = response.Content.Headers.ContentMD5;
                if (hash == null)
                {
                    return null;
                }

                return BitConverter
                    .ToString(hash)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
