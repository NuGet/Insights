using System.Collections.Generic;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class AutocompleteClient
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly HttpSource _httpSource;
        private readonly ILogger<AutocompleteClient> _logger;

        public AutocompleteClient(ServiceIndexCache serviceIndexCache, HttpSource httpSource, ILogger<AutocompleteClient> logger)
        {
            _serviceIndexCache = serviceIndexCache;
            _httpSource = httpSource;
            _logger = logger;
        }

        public async Task<AutocompleteIdResults> GetIdsAsync(string q, int? skip, int? take, bool? prerelease, string semVerLevel)
        {
            var queryString = new Dictionary<string, string>();

            if (q != null)
            {
                queryString["q"] = q;
            }
            
            if (skip.HasValue)
            {
                queryString["skip"] = skip.Value.ToString();
            }
            
            if (take.HasValue)
            {
                queryString["take"] = take.ToString();
            }
            
            if (prerelease.HasValue)
            {
                queryString["prerelease"] = prerelease.Value ? "true" : "false";
            }
            
            if (semVerLevel != null)
            {
                queryString["semVerLevel"] = semVerLevel;
            }

            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.Autocomplete);
            var url = QueryHelpers.AddQueryString(baseUrl, queryString);

            return await _httpSource.DeserializeUrlAsync<AutocompleteIdResults>(
                url,
                ignoreNotFounds: false,
                logger: _logger);
        }

        public async Task<AutocompleteVersionResults> GetVersionsAsync(string id, bool? prerelease, string semVerLevel)
        {
            var queryString = new Dictionary<string, string>();
            queryString["id"] = id;

            if (prerelease.HasValue)
            {
                queryString["prerelease"] = prerelease.Value ? "true" : "false";
            }

            if (semVerLevel != null)
            {
                queryString["semVerLevel"] = semVerLevel;
            }

            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.Autocomplete);
            var url = QueryHelpers.AddQueryString(baseUrl, queryString);

            return await _httpSource.DeserializeUrlAsync<AutocompleteVersionResults>(
                url,
                ignoreNotFounds: false,
                logger: _logger);
        }
    }
}
