// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Xunit;
using Xunit.Abstractions;
using static NuGet.Insights.BaseLogicIntegrationTest;

namespace NuGet.Insights
{
    public class HttpSourceExtensionsTest
    {
        public class TheDeserializeUrlAsyncMethod : HttpSourceExtensionsTest
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

                var result = await Target.DeserializeUrlAsync<PersonWithDateTimeOffset>(TestUrl, IgnoreNotFounds, Logger);

                Assert.Equal(TimeSpan.Parse(expected), result.DateOfBirth.Offset);
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

                var result = await Target.DeserializeUrlAsync<PersonWithDateTime>(TestUrl, IgnoreNotFounds, Logger);

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

            public TheDeserializeUrlAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }
        }

        public HttpSourceExtensionsTest(ITestOutputHelper output)
        {
            Output = output;
            Logger = output.GetLogger<HttpSource>();
            HttpMessageHandlerFactory = new TestHttpMessageHandlerFactory();
            TestUrl = "https://api.example.com/v3/test";
            IgnoreNotFounds = false;
            Target = new HttpSource(
                new PackageSource("https://api.example.com/v3/index.json"),
                () =>
                {
                    var httpMessageHandler = HttpMessageHandlerFactory.Create();
                    var resource = new HttpMessageHandlerResource(httpMessageHandler);
                    return Task.FromResult<HttpHandlerResource>(resource);
                },
                NullThrottle.Instance);
        }

        public ITestOutputHelper Output { get; }
        public ILogger<HttpSource> Logger { get; }
        public TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public string TestUrl { get; }
        public bool IgnoreNotFounds { get; }
        public HttpSource Target { get; }

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
    }
}
