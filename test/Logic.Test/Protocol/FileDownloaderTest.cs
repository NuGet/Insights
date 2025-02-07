// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class FileDownloaderTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task CanAllowMissingContentLength()
        {
            // Arrange
            SetNoContentLength();

            // Act
            var result = await FileDownloader.DownloadUrlToFileAsync(
                url: "http://example/favicon.ico",
                getTempFileName: () => $"{Guid.NewGuid():N}-favicon.ico",
                getHasher: IncrementalHash.CreateNone,
                requireContentLength: false,
                token: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            await using var body = result.Value.Body;
            Assert.Equal("Hello, world!", await new StreamReader(body.Stream).ReadToEndAsync());
            Assert.Empty(result.Value.Headers["Content-Length"]);
        }

        [Fact]
        public async Task CanRejectMissingContentLength()
        {
            // Arrange
            SetNoContentLength();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await FileDownloader.DownloadUrlToFileAsync(
                    url: "http://example/favicon.ico",
                    getTempFileName: () => $"{Guid.NewGuid():N}-favicon.ico",
                    getHasher: IncrementalHash.CreateNone,
                    token: CancellationToken.None);
            });
        }

        private void SetNoContentLength()
        {
            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("Hello, world!")));
                response.Content.Headers.ContentLength = null;
                return Task.FromResult(response);
            };
        }

        public FileDownloader FileDownloader => Host.Services.GetRequiredService<FileDownloader>();

        public FileDownloaderTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
