using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class RemoteCursorReader
    {
        private readonly HttpSource _httpSource;
        private readonly ILogger _log;

        public RemoteCursorReader(HttpSource httpSource, ILogger log)
        {
            _httpSource = httpSource;
            _log = log;
        }

        public async Task<IReadOnlyList<Cursor>> GetNuGetOrgCursors(CancellationToken token)
        {
            var cursorInput = new Dictionary<string, string>
            {
                { CursorNames.NuGetOrg.FlatContainerGlobal, "http://nugetgallery.blob.core.windows.net/v3-flatcontainer/cursor.json" },
                { CursorNames.NuGetOrg.FlatContainerChina, "http://nugetgalleryprod.blob.core.chinacloudapi.cn/v3-flatcontainer/cursor.json" },
            };

            var output = new List<Cursor>();
            foreach (var pair in cursorInput)
            {
                var value = await GetJsonCursorAsync(pair.Value, token);
                var cursor = new Cursor { Name = pair.Key };
                cursor.SetDateTimeOffset(value);
                output.Add(cursor);
            }

            return output;
        }

        public async Task<DateTimeOffset> GetJsonCursorAsync(string url, CancellationToken token)
        {
            var json = await _httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, _log),
                async stream =>
                {
                    using (var textReader = new StreamReader(stream))
                    {
                        return await textReader.ReadToEndAsync();
                    }
                },
                _log,
                token);

            var cursor = JsonConvert.DeserializeObject<JsonCursor>(
                json,
                new JsonSerializerSettings
                {
                    DateParseHandling = DateParseHandling.None,
                });

            return DateTimeOffset.Parse(
                cursor.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal);
        }

        private class JsonCursor
        {
            public string Value { get; set; }
        }
    }
}
