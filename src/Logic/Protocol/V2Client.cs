// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol;

#nullable enable

namespace NuGet.Insights
{
    public class V2Client
    {
        private readonly HttpSource _httpSource;
        private readonly V2Parser _parser;
        private readonly ILogger<V2Client> _logger;

        public V2Client(HttpSource httpSource, V2Parser parser, ILogger<V2Client> logger)
        {
            _httpSource = httpSource;
            _parser = parser;
            _logger = logger;
        }

        public async Task<V2Package?> GetPackageOrNullAsync(string baseUrl, string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var url = $"{baseUrl.TrimEnd('/')}/Packages(Id='{id}',Version='{normalizedVersion}')?hijack=false";
            var page = await ParseV2PageAsync(url);

            if (page.Count == 0)
            {
                return null;
            }
            else if (page.Count == 1)
            {
                return page[0];
            }

            throw new InvalidDataException("The number of packages returned by V2 should be either 0 or 1.");
        }

        private async Task<IReadOnlyList<V2Package>> ParseV2PageAsync(string url)
        {
            var nuGetLogger = _logger.ToNuGetLogger();
            return await _httpSource.ProcessStreamWithRetryAsync(
                new HttpSourceRequest(url, nuGetLogger)
                {
                    IgnoreNotFounds = true,
                },
                stream =>
                {
                    if (stream == null)
                    {
                        return Task.FromResult<IReadOnlyList<V2Package>>(Array.Empty<V2Package>());
                    }

                    var document = XmlUtility.LoadXml(stream);
                    var page = _parser.ParsePage(document);
                    return Task.FromResult(page);
                },
                _logger,
                CancellationToken.None);
        }
    }
}
