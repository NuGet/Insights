using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class RemoteCursorService
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly HttpSource _httpSource;
        private readonly SearchServiceCursorReader _searchServiceCursorReader;
        private readonly CursorService _cursorService;
        private readonly ILogger<RemoteCursorService> _logger;

        public RemoteCursorService(
            ServiceIndexCache serviceIndexCache,
            HttpSource httpSource,
            SearchServiceCursorReader searchServiceCursorReader,
            CursorService cursorService,
            ILogger<RemoteCursorService> logger)
        {
            _serviceIndexCache = serviceIndexCache;
            _httpSource = httpSource;
            _searchServiceCursorReader = searchServiceCursorReader;
            _cursorService = cursorService;
            _logger = logger;
        }

        public async Task UpdateNuGetOrgCursors(CancellationToken token)
        {
            var output = new List<CursorEntity>();

            // Read the JSON cursors.
            var flatContainerBaseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var registrationBaseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.RegistrationOriginal);
            var jsonCursorInput = new Dictionary<string, string>
            {
                { CursorNames.NuGetOrg.FlatContainer, $"{flatContainerBaseUrl.TrimEnd('/')}/cursor.json"  },
                { CursorNames.NuGetOrg.Registration, $"{registrationBaseUrl.TrimEnd('/')}/cursor.json" },
            };

            foreach (var pair in jsonCursorInput)
            {
                var value = await GetJsonCursorAsync(pair.Value, token);
                await _cursorService.SetValueAsync(pair.Key, value);
            }

            // Read the search service cursor.
            var searchServiceCursor = await _searchServiceCursorReader.GetCursorAsync();
            await _cursorService.SetValueAsync(CursorNames.NuGetOrg.Search, searchServiceCursor);
        }

        public async Task<DateTimeOffset> GetJsonCursorAsync(string url, CancellationToken token)
        {
            var cursor = await _httpSource.DeserializeUrlAsync<JsonCursor>(
                url,
                ignoreNotFounds: false,
                logger: _logger);

            return cursor.Value;
        }

        private class JsonCursor
        {
            [JsonProperty("value")]
            public DateTimeOffset Value { get; set; }
        }
    }
}
