using System;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2ToDatabaseProcessor
    {
        private const int PageSize = 100;
        private readonly CursorService _cursorService;
        private readonly V2Client _v2Client;
        private readonly V2PackageEntityService _service;
        private readonly ExplorePackagesSettings _settings;

        public V2ToDatabaseProcessor(
            CursorService cursorService,
            V2Client v2Client,
            V2PackageEntityService service,
            ExplorePackagesSettings settings)
        {
            _cursorService = cursorService;
            _v2Client = v2Client;
            _service = service;
            _settings = settings;
        }

        public async Task UpdateAsync()
        {
            var cursor = await _cursorService.GetAsync(CursorNames.V2ToDatabase);
            var start = cursor;
            if (cursor > DateTimeOffset.MinValue.AddHours(1))
            {
                start = start.AddHours(-1);
            }

            int packageCount;
            do
            {
                var packages = await _v2Client.GetPackagesAsync(
                   _settings.V2BaseUrl,
                   V2OrderByTimestamp.Created,
                   start,
                   PageSize);

                // If we have a full page, take only packages with a created timestamp less than the max.
                if (packages.Count == PageSize)
                {
                    var max = packages.Max(x => x.Created);
                    var packagesBeforeMax = packages
                        .Where(x => x.Created < max)
                        .ToList();

                    if (packages.Any()
                        && !packagesBeforeMax.Any())
                    {
                        throw new InvalidOperationException("All of the packages in the page have the same created timestamp.");
                    }

                    packages = packagesBeforeMax;
                }
                
                await _service.AddOrUpdatePackagesAsync(packages);

                packageCount = packages.Count;
                start = packages.Max(x => x.Created);

                if (start > cursor)
                {
                    cursor = start;
                    await _cursorService.SetAsync(CursorNames.V2ToDatabase, cursor);
                }
            }
            while (packageCount > 0);
        }
    }
}
