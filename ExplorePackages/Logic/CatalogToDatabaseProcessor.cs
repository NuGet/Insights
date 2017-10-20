using System.Collections.Generic;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using NuGet.CatalogReader;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogToDatabaseProcessor : ICatalogEntriesProcessor
    {
        private readonly ILogger _log;

        public CatalogToDatabaseProcessor(ILogger log)
        {
            _log = log;
        }

        public IReadOnlyList<string> DependencyCursorNames => new List<string>();
        public string CursorName => CursorNames.Catalog2Database;

        public async Task ProcessAsync(IReadOnlyList<CatalogEntry> entries)
        {
            using (var entityContext = new EntityContext())
            {
                var packageService = new PackageService(entityContext, _log);
                await packageService.AddOrUpdateBatchAsync(entries);
            }
        }
    }
}
