using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Support;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class RemoteCursorReader
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly HttpSource _httpSource;
        private readonly SearchServiceCursorReader _searchServiceCursorReader;
        private readonly ILogger _log;

        public RemoteCursorReader(
            ServiceIndexCache serviceIndexCache,
            HttpSource httpSource,
            SearchServiceCursorReader searchServiceCursorReader,
            ILogger log)
        {
            _serviceIndexCache = serviceIndexCache;
            _httpSource = httpSource;
            _searchServiceCursorReader = searchServiceCursorReader;
            _log = log;
        }

        public async Task<IReadOnlyList<Cursor>> GetNuGetOrgCursors(CancellationToken token)
        {
            var output = new List<Cursor>();

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
                AddCursor(output, pair.Key, value);
            }

            // Read the search service cursor.
            var searchServiceCursor = await _searchServiceCursorReader.GetCursorAsync();
            AddCursor(output, CursorNames.NuGetOrg.Search, searchServiceCursor);

            return output;
        }

        private static void AddCursor(List<Cursor> output, string name, DateTimeOffset value)
        {
            var cursor = new Cursor { Name = name };
            cursor.SetDateTimeOffset(value);
            output.Add(cursor);
        }

        public async Task<DateTimeOffset> GetJsonCursorAsync(string url, CancellationToken token)
        {
            var cursor = await _httpSource.DeserializeUrlAsync<JsonCursor>(
                url,
                ignoreNotFounds: false,
                log: _log);

            return cursor.Value;
        }

        private class JsonCursor
        {
            [JsonProperty("value")]
            public DateTimeOffset Value { get; set; }
        }
    }
}
