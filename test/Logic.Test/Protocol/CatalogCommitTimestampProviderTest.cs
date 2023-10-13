// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class CatalogCommitTimestampProviderTest : BaseLogicIntegrationTest
    {
        private const string IndexUrl = "https://api.nuget.org/v3/catalog0/index.json";
        private const string Page0Url = "https://example.com/catalog/page0.json";
        private const string Page1Url = "https://example.com/catalog/page1.json";
        private const string Page2Url = "https://example.com/catalog/page2.json";

        private static readonly DateTimeOffset TS0 = DateTimeOffset.Parse("2020-01-01Z");
        private static readonly DateTimeOffset TS1 = DateTimeOffset.Parse("2020-01-02Z");
        private static readonly DateTimeOffset TS2 = DateTimeOffset.Parse("2020-01-03Z");
        private static readonly DateTimeOffset TS3 = DateTimeOffset.Parse("2020-01-04Z");
        private static readonly DateTimeOffset TS4 = DateTimeOffset.Parse("2020-01-05Z");
        private static readonly DateTimeOffset TS5 = DateTimeOffset.Parse("2020-01-06Z");
        private static readonly DateTimeOffset TS6 = DateTimeOffset.Parse("2020-01-07Z");

        public CatalogCommitTimestampProviderTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            Index = new CatalogIndex
            {
                CommitTimestamp = TS1,
                Items = new List<CatalogPageItem>
                {
                    new CatalogPageItem { Url = Page0Url, CommitTimestamp = TS1 }
                }
            };
            Page0 = new CatalogPage
            {
                CommitTimestamp = TS1,
                Url = Page0Url,
                Items = new List<CatalogLeafItem>
                {
                    new CatalogLeafItem { Type = CatalogLeafType.PackageDetails, CommitTimestamp = TS0 },
                    new CatalogLeafItem { Type = CatalogLeafType.PackageDetails, CommitTimestamp = TS1 },
                }
            };
            Page1 = new CatalogPage
            {
                CommitTimestamp = TS4,
                Url = Page1Url,
                Items = new List<CatalogLeafItem>
                {
                    new CatalogLeafItem { Type = CatalogLeafType.PackageDetails, CommitTimestamp = TS2 },
                    new CatalogLeafItem { Type = CatalogLeafType.PackageDetails, CommitTimestamp = TS3 },
                    new CatalogLeafItem { Type = CatalogLeafType.PackageDetails, CommitTimestamp = TS4 },
                }
            };
            Page2 = new CatalogPage
            {
                CommitTimestamp = TS6,
                Url = Page2Url,
                Items = new List<CatalogLeafItem>
                {
                    new CatalogLeafItem { Type = CatalogLeafType.PackageDetails, CommitTimestamp = TS5 },
                    new CatalogLeafItem { Type = CatalogLeafType.PackageDetails, CommitTimestamp = TS6 },
                }
            };

            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) =>
            {
                string json;
                switch (r.RequestUri.AbsoluteUri)
                {
                    case IndexUrl:
                        json = JsonSerializer.Serialize(Index, HttpSourceExtensions.JsonSerializerOptions);
                        break;
                    case Page0Url:
                        json = Page0 == null ? null : JsonSerializer.Serialize(Page0, HttpSourceExtensions.JsonSerializerOptions);
                        break;
                    case Page1Url:
                        json = Page1 == null ? null : JsonSerializer.Serialize(Page1, HttpSourceExtensions.JsonSerializerOptions);
                        break;
                    case Page2Url:
                        json = Page2 == null ? null : JsonSerializer.Serialize(Page2, HttpSourceExtensions.JsonSerializerOptions);
                        break;
                    default:
                        json = null;
                        break;
                }

                return Task.FromResult(new HttpResponseMessage(json == null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
                {
                    RequestMessage = r,
                    Content = new StringContent(json ?? string.Empty)
                });
            };
        }

        public CatalogIndex Index { get; set; }
        public CatalogPage Page0 { get; set; }
        public CatalogPage Page1 { get; set; }
        public CatalogPage Page2 { get; set; }

        public CatalogCommitTimestampProvider Target => Host.Services.GetRequiredService<CatalogCommitTimestampProvider>();

        [Fact]
        public async Task ReturnsNullIfInputIsEqualToMax()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(TS1);

            // Assert
            Assert.Null(min);
            var request = Assert.Single(HttpMessageHandlerFactory.SuccessRequests);
            Assert.Equal(IndexUrl, request.RequestUri.AbsoluteUri);
        }

        [Fact]
        public async Task ReturnsNullIfInputIsGreaterThanMax()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(TS2);

            // Assert
            Assert.Null(min);
            var request = Assert.Single(HttpMessageHandlerFactory.SuccessRequests);
            Assert.Equal(IndexUrl, request.RequestUri.AbsoluteUri);
        }

        [Fact]
        public async Task ReturnsMaxIfInputIsLessThanMax()
        {
            // Arrange & Act
            var min = await Target.GetNextAsync(TS0);

            // Assert
            Assert.Equal(TS1, min);
            Assert.Equal(2, HttpMessageHandlerFactory.SuccessRequests.Count());
            Assert.Equal(1, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == IndexUrl));
            Assert.Equal(1, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == Page0Url));
        }

        [Fact]
        public async Task RefreshesIndexCacheIfMaxIsGreaterThanCachedPage()
        {
            // Arrange
            await Target.GetNextAsync(TS0);

            // Add a second page, containing TS2, TS3, and TS4
            Index.CommitTimestamp = TS4;
            Index.Items.Add(new CatalogPageItem { Url = Page1Url, CommitTimestamp = TS4 });

            // Act
            var min = await Target.GetNextAsync(TS1);

            // Assert
            Assert.Equal(TS2, min);
            Assert.Equal(4, HttpMessageHandlerFactory.SuccessRequests.Count());
            Assert.Equal(2, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == IndexUrl));
            Assert.Equal(1, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == Page0Url));
            Assert.Equal(1, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == Page1Url));
        }

        [Fact]
        public async Task RefreshesIndexCacheIfMaxIsGreaterThanCachedPageWithTwoPages()
        {
            // Arrange
            await Target.GetNextAsync(TS0);

            // Add a second page, containing TS2, TS3, and TS4
            Index.Items.Add(new CatalogPageItem { Url = Page1Url, CommitTimestamp = TS4 });

            // Add a third page, containing TS5 and TS6
            Index.CommitTimestamp = TS6;
            Index.Items.Add(new CatalogPageItem { Url = Page2Url, CommitTimestamp = TS6 });

            // Act
            var min = await Target.GetNextAsync(TS5);

            // Assert
            Assert.Equal(TS6, min);
            Assert.Equal(4, HttpMessageHandlerFactory.SuccessRequests.Count());
            Assert.Equal(2, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == IndexUrl));
            Assert.Equal(1, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == Page0Url));
            Assert.Equal(1, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == Page2Url));
        }

        [Fact]
        public async Task RefreshesPageCacheIfMaxIsGreaterThanCachedPage()
        {
            // Arrange
            await Target.GetNextAsync(TS0);

            // Extend the first page to contain TS2
            Index.CommitTimestamp = TS2;
            Index.Items[0].CommitTimestamp = TS2;
            Page0.CommitTimestamp = TS2;
            Page0.Items.Add(new CatalogLeafItem { Type = CatalogLeafType.PackageDetails, CommitTimestamp = TS2 });

            // Act
            var min = await Target.GetNextAsync(TS1);

            // Assert
            Assert.Equal(TS2, min);
            Assert.Equal(4, HttpMessageHandlerFactory.SuccessRequests.Count());
            Assert.Equal(2, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == IndexUrl));
            Assert.Equal(2, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == Page0Url));
        }

        [Fact]
        public async Task RefreshesIndexCacheIfMaxIsGreaterThanIndex()
        {
            // Arrange
            await Target.GetNextAsync(TS0);

            // Act
            var min = await Target.GetNextAsync(TS1);

            // Assert
            Assert.Null(min);
            Assert.Equal(3, HttpMessageHandlerFactory.SuccessRequests.Count());
            Assert.Equal(2, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == IndexUrl));
            Assert.Equal(1, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsoluteUri == Page0Url));
        }

        [Fact]
        public async Task DoesNotRefreshIndexWhenMinIsLowerThanIndex()
        {
            // Arrange
            await Target.GetNextAsync(TS0);
            HttpMessageHandlerFactory.Clear();

            // Act
            var min = await Target.GetNextAsync(TS0);

            // Assert
            Assert.Equal(TS1, min);
            Assert.Empty(HttpMessageHandlerFactory.Responses);
        }

        [Fact]
        public async Task OnlyLoadsPagesLazily()
        {
            // Arrange

            // Add a second page, containing TS2, TS3, and TS4
            Index.Items.Add(new CatalogPageItem { Url = Page1Url, CommitTimestamp = TS4 });

            await Target.GetNextAsync(TS3);
            HttpMessageHandlerFactory.Clear();

            // Act
            var min = await Target.GetNextAsync(TS0);

            // Assert
            Assert.Equal(TS1, min);
            var request = Assert.Single(HttpMessageHandlerFactory.SuccessRequests);
            Assert.Equal(Page0Url, request.RequestUri.AbsoluteUri);
        }

        [Fact]
        public async Task RejectsUnexpectedChangeOfLastPageItem()
        {
            // Arrange
            await Target.GetNextAsync(TS0);

            Index.CommitTimestamp = TS2;
            Index.Items[0].Url = Page1Url;
            Index.Items[0].CommitTimestamp = TS2;

            // Act & Asset
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => Target.GetNextAsync(TS1));
            Assert.StartsWith(
                "The provided catalog page item URL 'https://example.com/catalog/page1.json' does not match the expected value 'https://example.com/catalog/page0.json'.",
                ex.Message);
        }

        [Fact]
        public async Task RejectsUnexpectedUrlChangeOfNonLastPageItem()
        {
            // Arrange
            Index.CommitTimestamp = TS4;
            Index.Items.Add(new CatalogPageItem { Url = Page1Url, CommitTimestamp = TS4 });

            await Target.GetNextAsync(TS0);

            Index.CommitTimestamp = TS5;
            Index.Items[0].Url = Page1Url;

            // Act & Asset
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => Target.GetNextAsync(TS5));
            Assert.StartsWith(
                "The page at index 0 has a URL 'https://example.com/catalog/page1.json' that is different than before 'https://example.com/catalog/page0.json'.",
                ex.Message);
        }

        [Fact]
        public async Task RejectsUnexpectedTimestampChangeOfNonLastPageItem()
        {
            // Arrange
            Index.CommitTimestamp = TS4;
            Index.Items.Add(new CatalogPageItem { Url = Page1Url, CommitTimestamp = TS4 });

            await Target.GetNextAsync(TS0);

            Index.CommitTimestamp = TS5;
            Index.Items[0].CommitTimestamp = TS4;

            // Act & Asset
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => Target.GetNextAsync(TS5));
            Assert.StartsWith(
                "The page at index 0 has a commit timestamp '2020-01-05T00:00:00.0000000+00:00' that is different than before '2020-01-02T00:00:00.0000000+00:00'.",
                ex.Message);
        }
    }
}
