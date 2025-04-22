// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Mode = NuGet.Insights.FileSystemHttpCache.FileSystemHttpCacheMode;

namespace NuGet.Insights.FileSystemHttpCache
{
    public class FileSystemHttpCacheHandlerTests : BaseLogicIntegrationTest
    {
        public enum Result
        {
            Request1,
            Request2,
            Exception,
        }

        public enum Cache
        {
            Request1,
            Request2,
            Missing,
        }

        /// <summary>
        /// Checks both cache-bust normalization and failed request replacement, for <see cref="FileDownloader"/>.
        /// </summary>
        [Theory]
        [InlineData(Mode.ReadOnly, Result.Request1, Cache.Request1, 1)]
        [InlineData(Mode.ReadAndWriteOnlyMissing, Result.Request2, Cache.Request2, 2)]
        [InlineData(Mode.WriteOnlyMissing, Result.Request2, Cache.Request2, 2)]
        [InlineData(Mode.WriteAlways, Result.Request2, Cache.Request2, 2)]
        public async Task ReplacesCachedFailedRequest(Mode mode, Result result, Cache cache, int requestCount)
        {
            // Arrange
            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) =>
            {
                HttpStatusCode statusCode;
                if (r.RequestUri.Query?.Contains("cache-bust=", StringComparison.Ordinal) == false)
                {
                    statusCode = HttpStatusCode.BadRequest;
                }
                else
                {
                    statusCode = HttpStatusCode.OK;
                }

                var response = new HttpResponseMessage(statusCode)
                {
                    RequestMessage = r,
                    Content = new StringContent("Request " + Interlocked.Increment(ref RequestCount)),
                };
                return Task.FromResult(response);
            };
            await GetResultAsync(Mode.WriteAlways);

            // Act
            var actualResult = await GetResultAsync(mode, "https://api.nuget.org/foo.txt?cache-bust=yes-plz");

            // Assert
            var actualCache = GetCache();
            Assert.Equal((result, cache), (actualResult, actualCache));
            Assert.Equal(requestCount, RequestCount);
        }

        [Theory]
        [InlineData(Mode.ReadAndWriteOnlyMissing, Result.Request1, Cache.Request1)]
        [InlineData(Mode.ReadOnly, Result.Request1, Cache.Request1)]
        [InlineData(Mode.WriteOnlyMissing, Result.Request2, Cache.Request1)]
        [InlineData(Mode.WriteAlways, Result.Request2, Cache.Request2)]
        public async Task ModeWithCachedResponse(Mode mode, Result result, Cache cache)
        {
            // Arrange
            await GetResultAsync(Mode.WriteAlways);

            // Act
            var actualResult = await GetResultAsync(mode);

            // Assert
            var actualCache = GetCache();
            Assert.Equal((result, cache), (actualResult, actualCache));
        }

        [Theory]
        [InlineData(Mode.ReadAndWriteOnlyMissing, Result.Request1, Cache.Request1)]
        [InlineData(Mode.ReadOnly, Result.Exception, Cache.Missing)]
        [InlineData(Mode.WriteOnlyMissing, Result.Request1, Cache.Request1)]
        [InlineData(Mode.WriteAlways, Result.Request1, Cache.Request1)]
        public async Task ModeWithNoCachedResponse(Mode mode, Result result, Cache cache)
        {
            // Act
            var actualResult = await GetResultAsync(mode);

            // Assert
            var actualCache = GetCache();
            Assert.Equal((result, cache), (actualResult, actualCache));
        }

        [Fact]
        public async Task ExpectedCacheContent()
        {
            // Act
            await GetResultAsync(Mode.WriteAlways);

            // Assert
            AssertExpectedCacheContent();
        }

        [Fact]
        public async Task RecreatesRedirectMessage()
        {
            // Arrange
            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) =>
            {
                // automatic redirect modifies the request in place
                r.RequestUri = new Uri("https://api.nuget.org/foo-redirect.txt");

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = r,
                    Content = new StringContent("Request " + Interlocked.Increment(ref RequestCount)),
                };
                response.Headers.TryAddWithoutValidation("ETag", "my-silly-blob-storage-etag");
                return Task.FromResult(response);
            };
            await GetResultAsync(Mode.WriteAlways);

            // Act
            var result = await GetResultAsync(Mode.WriteOnlyMissing);

            // Assert
            Assert.Equal((Result.Request2, Cache.Request1), (result, GetCache()));
            Assert.Equal($"""
                GET https://api.nuget.org/foo-redirect.txt HTTP/1.1
                If-Match: "my-silly-blob-storage-etag"
                {FileSystemHttpCacheHandler.CacheHeaderName}: {FileSystemHttpCacheHandler.CacheHeaderValue}

                """, File.ReadAllText(GetCachePath("rqh.txt")));
        }

        [Fact]
        public async Task RemovesCacheBustQueryString()
        {
            // Act
            await GetResultAsync(Mode.WriteAlways, "https://api.nuget.org/foo.txt?cache-bust=yes-plz");

            // Assert
            AssertExpectedCacheContent();
        }

        private void AssertExpectedCacheContent()
        {
            Assert.Equal("""
                {
                  "method": "GET",
                  "url": "https://api.nuget.org/foo.txt",
                  "headers": [
                    {
                      "name": "if-match",
                      "value": "\u0022my-silly-blob-storage-etag\u0022"
                    }
                  ]
                }
                """.Replace("\r", "", StringComparison.Ordinal), File.ReadAllText(GetCachePath("i.json")));
            Assert.Equal($"""
                GET https://api.nuget.org/foo.txt HTTP/1.1
                If-Match: "my-silly-blob-storage-etag"
                {FileSystemHttpCacheHandler.CacheHeaderName}: {FileSystemHttpCacheHandler.CacheHeaderValue}

                """, File.ReadAllText(GetCachePath("rqh.txt")));
            Assert.Equal("""
                Request 1
                """, File.ReadAllText(GetCachePath("rsb.txt")));
            Assert.Equal($"""
                HTTP/1.1 200 OK
                ETag: "my-silly-blob-storage-etag"
                {FileSystemHttpCacheHandler.CacheHeaderName}: {FileSystemHttpCacheHandler.CacheHeaderValue}
                Content-Type: text/plain; charset=utf-8
                
                """, File.ReadAllText(GetCachePath("rsh.txt")));
        }

        private async Task<Result> GetResultAsync(Mode mode, string url = "https://api.nuget.org/foo.txt")
        {
            try
            {
                using var httpClient = GetHttpClient(mode);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("If-Match", "my-silly-blob-storage-etag");
                using var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                return responseBody switch
                {
                    "Request 1" => Result.Request1,
                    "Request 2" => Result.Request2,
                    _ => throw new NotImplementedException("Unexpected response: " + response),
                };
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Cannot send request to the network when", StringComparison.Ordinal))
            {
                return Result.Exception;
            }
        }

        private Cache GetCache()
        {
            var responseBodyPath = GetCachePath("rsb.txt");
            if (!File.Exists(responseBodyPath))
            {
                return Cache.Missing;
            }

            var response = File.ReadAllText(responseBodyPath);
            return response switch
            {
                "Request 1" => Cache.Request1,
                "Request 2" => Cache.Request2,
                _ => throw new NotImplementedException("Unexpected response: " + response),
            };
        }

        private string GetCachePath(string suffix)
        {
            var cacheDir = Path.Combine(TestDirectory.FullPath, "https_api.nuget.org", "foo.txt");
            var cacheFilePrefix = "G_v7o7u4i_";
            var responseBodyPath = Path.Combine(cacheDir, $"{cacheFilePrefix}{suffix}");
            return responseBodyPath;
        }

        private HttpClient GetHttpClient(Mode mode)
        {
            var handler = FileSystemHttpCacheIntegrationTestSettings.Create(TestDirectory.FullPath, mode);
            handler.InnerHandler = HttpMessageHandlerFactory.Create();
            return new HttpClient(handler);
        }

        protected override Task DisposeInternalAsync()
        {
            TestDirectory.Dispose();
            return base.DisposeInternalAsync();
        }

        public TestDirectory TestDirectory { get; }
        public int RequestCount;

        public FileSystemHttpCacheHandlerTests(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            TestDirectory = TestDirectory.Create();

            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = r,
                    Content = new StringContent("Request " + Interlocked.Increment(ref RequestCount)),
                };
                response.Headers.TryAddWithoutValidation("ETag", "my-silly-blob-storage-etag");
                return Task.FromResult(response);
            };
        }
    }
}
