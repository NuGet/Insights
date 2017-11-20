using System;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class V2ToDatabaseProcessor
    {
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
            var start = await _cursorService.GetAsync(CursorNames.V2ToDatabase);

            int addedCount;
            int packageCount;
            var caughtUp = false;
            do
            {
                var startMinusDelta = start;
                if (startMinusDelta > DateTimeOffset.UtcNow.AddHours(-1))
                {
                    startMinusDelta = start.AddHours(-1);
                    caughtUp = true;
                }

                var packages = await _v2Client.GetPackagesAsync(
                   _settings.V2BaseUrl,
                   V2OrderByTimestamp.Created,
                   startMinusDelta);
                packageCount = packages.Count;

                if (packages.Any())
                {
                    start = packages.Max(x => x.Created);
                }

                addedCount = await _service.AddOrUpdatePackagesAsync(packages);

                await _cursorService.SetAsync(CursorNames.V2ToDatabase, start);
            }
            while (addedCount > 0 || packageCount > 0 || !caughtUp);
        }
    }
}
