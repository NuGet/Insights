// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class HttpClientExtensionsTest : BaseLogicIntegrationTest
    {
        public class TheProcessResponseWithRetryAsyncMethod : HttpClientExtensionsTest
        {
            public TheProcessResponseWithRetryAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task DoesNotRetryForOtherExceptionWhenReadingStream()
            {
                SetStreamException(() => new InvalidOperationException("It's not a big truck!"));

                var ex = await AssertThrowsAfterReadAsStreamAsync<InvalidOperationException>();

                Assert.Equal("It's not a big truck!", ex.Message);
                Assert.Single(HttpMessageHandlerFactory.Responses);
            }

            [Fact]
            public async Task DoesNotRetryForOtherExceptionWhenReadingString()
            {
                SetStreamException(() => new InvalidOperationException("It's not a big truck!"));

                var ex = await AssertThrowsAfterReadAsStringAsync<InvalidOperationException>();

                Assert.Equal("It's not a big truck!", ex.Message);
                Assert.Single(HttpMessageHandlerFactory.Responses);
            }

            [Fact]
            public async Task RetriesForHttpRequestExceptionWithInnerIOException()
            {
                SetStreamException(() => new IOException("It's not a big truck!"));

                var ex = await AssertThrowsAfterReadAsStringAsync<HttpRequestException>();

                var innerEx = Assert.IsType<IOException>(ex.InnerException);
                Assert.Equal("It's not a big truck!", innerEx.Message);
                Assert.Equal(3, HttpMessageHandlerFactory.Responses.Count());
            }

            [Fact]
            public async Task RetriesForIOException()
            {
                SetStreamException(() => new IOException("It's not a big truck!"));

                var ex = await AssertThrowsAfterReadAsStreamAsync<IOException>();

                Assert.Equal("It's not a big truck!", ex.Message);
                Assert.Equal(3, HttpMessageHandlerFactory.Responses.Count());
            }

            [Fact]
            public async Task RetriesForHttpRequestExceptionWithInnerOperationCanceledException()
            {
                SetStreamException(() => new OperationCanceledException("It's not a big truck!"));

                var ex = await AssertThrowsAfterReadAsStringAsync<OperationCanceledException>();

                Assert.Equal("It's not a big truck!", ex.Message);
                Assert.Equal(3, HttpMessageHandlerFactory.Responses.Count());
            }

            [Fact]
            public async Task RetriesForOperationCanceledException()
            {
                SetStreamException(() => new OperationCanceledException("It's not a big truck!"));

                var ex = await AssertThrowsAfterReadAsStreamAsync<OperationCanceledException>();

                Assert.Equal("It's not a big truck!", ex.Message);
                Assert.Equal(3, HttpMessageHandlerFactory.Responses.Count());
            }

            private async Task<T> AssertThrowsAfterReadAsStringAsync<T>() where T : Exception
            {
                return await Assert.ThrowsAsync<T>(() => Target.ProcessResponseWithRetriesAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, "https://example.com/v3/index.json"),
                    response => response.Content.ReadAsStringAsync(),
                    Logger,
                    CancellationToken.None));
            }

            private async Task<T> AssertThrowsAfterReadAsStreamAsync<T>() where T : Exception
            {
                return await Assert.ThrowsAsync<T>(() => Target.ProcessResponseWithRetriesAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, "https://example.com/v3/index.json"),
                    async response =>
                    {
                        using var stream = response.Content.ReadAsStream();
                        using var destination = new MemoryStream();
                        await stream.CopyToAsync(destination);
                        return destination;
                    },
                    Logger,
                    CancellationToken.None));
            }
        }

        public class TheDeserializeUrlAsyncMethod : HttpClientExtensionsTest
        {
            [Theory]
            [InlineData("", "00:00:00")]
            [InlineData("Z", "00:00:00")]
            [InlineData("+00:00", "00:00:00")]
            [InlineData("-00:00", "00:00:00")]
            [InlineData("-08:00", "-08:00:00")]
            [InlineData("+08:00", "08:00:00")]
            public async Task ParsesDateTimeOffsetWithUTCDefault(string suffix, string expected)
            {
                SetTestUrlContent(@$"{{""Name"": ""Bill Gates"", ""DateOfBirth"": ""1955-10-28T00:00:00{suffix}""}}");

                var result = await Target.DeserializeUrlAsync<PersonWithDateTimeOffset>(TestUrl, Logger);

                Assert.Equal(TimeSpan.Parse(expected, CultureInfo.InvariantCulture), result.DateOfBirth.Offset);
                Assert.Equal(DateTimeKind.Unspecified, result.DateOfBirth.DateTime.Kind);
                Assert.Equal("1955-10-28T00:00:00.0000000", result.DateOfBirth.DateTime.ToString("O"));
            }

            [Theory]
            [InlineData("", "1955-10-28T00:00:00.0000000Z")]
            [InlineData("Z", "1955-10-28T00:00:00.0000000Z")]
            [InlineData("+00:00", "1955-10-28T00:00:00.0000000Z")]
            [InlineData("-00:00", "1955-10-28T00:00:00.0000000Z")]
            [InlineData("-08:00", "1955-10-28T08:00:00.0000000Z")]
            [InlineData("+08:00", "1955-10-27T16:00:00.0000000Z")]
            public async Task ParsesDateTimeWithUTCDefault(string suffix, string expected)
            {
                SetTestUrlContent(@$"{{""Name"": ""Bill Gates"", ""DateOfBirth"": ""1955-10-28T00:00:00{suffix}""}}");

                var result = await Target.DeserializeUrlAsync<PersonWithDateTime>(TestUrl, Logger);

                Assert.Equal(DateTimeKind.Utc, result.DateOfBirth.Kind);
                Assert.Equal(expected, result.DateOfBirth.ToString("O"));
            }

            private void SetTestUrlContent(string content)
            {
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    await Task.Yield();

                    if (r.RequestUri.AbsoluteUri == TestUrl)
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(content),
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = new StringContent(string.Empty),
                    };
                };
            }

            public TheDeserializeUrlAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public string TestUrl { get; }
        public bool IgnoreNotFounds { get; }
        public HttpClient Target { get; }

        public HttpClientExtensionsTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            TestUrl = "https://api.example.com/v3/test";
            IgnoreNotFounds = false;
            Target = new HttpClient(HttpMessageHandlerFactory.Create());
        }

        private void SetStreamException(Func<Exception> getException)
        {
            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) =>
            {
                var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes("Hello!"));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(
                        new OnReadStream(memoryStream,
                        () => throw getException())),
                });
            };
        }

        public void Dispose()
        {
            Target.Dispose();
        }

        private class PersonWithDateTimeOffset
        {
            public string Name { get; set; }
            public DateTimeOffset DateOfBirth { get; set; }
        }

        private class PersonWithDateTime
        {
            public string Name { get; set; }
            public DateTime DateOfBirth { get; set; }
        }

        private class OnReadStream : Stream
        {
            private readonly Stream _inner;
            private readonly Action _onRead;

            public OnReadStream(Stream inner, Action onRead)
            {
                _inner = inner;
                _onRead = onRead;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position
            {
                get => _inner.Position;
                set => _inner.Position = value;
            }

            public override void Flush() => _inner.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                _onRead();
                return _inner.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        }
    }
}
