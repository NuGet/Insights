// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol;

#nullable enable

namespace NuGet.Insights
{
    public class RemoteCursorClient : IRemoteCursorClient
    {
        private readonly CatalogClient _catalogClient;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly HttpSource _httpSource;
        private readonly ILogger<RemoteCursorClient> _logger;

        public RemoteCursorClient(
            CatalogClient catalogClient,
            ServiceIndexCache serviceIndexCache,
            HttpSource httpSource,
            ILogger<RemoteCursorClient> logger)
        {
            _catalogClient = catalogClient;
            _serviceIndexCache = serviceIndexCache;
            _httpSource = httpSource;
            _logger = logger;
        }

        public async Task<DateTimeOffset> GetCatalogAsync(CancellationToken token = default)
        {
            return await _catalogClient.GetCommitTimestampAsync();
        }

        public async Task<DateTimeOffset> GetFlatContainerAsync(CancellationToken token = default)
        {
            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var cursorUrl = $"{baseUrl.TrimEnd('/')}/cursor.json";
            return await GetJsonCursorAsync(cursorUrl, token);
        }

        private async Task<DateTimeOffset> GetJsonCursorAsync(string url, CancellationToken token = default)
        {
            var cursor = await _httpSource.DeserializeUrlAsync<JsonCursor>(
                url,
                logger: _logger,
                token: token);

            return cursor.Value;
        }

        private class JsonCursor
        {
            [JsonPropertyName("value")]
            public DateTimeOffset Value { get; set; }
        }
    }
}
