# ExplorePackages

Explore packages on a V3 NuGet package source that has a catalog (NuGet.org!).

## Purpose

The purpose of this repository is to explore oddities and inconsistencies on NuGet.org's available packages. To support
this goal, I've built several generic ways of writing "queries" which search for packages with arbitrary characteristics.

### Parallelized queries on Azure Functions

- `FindLatestLeaves` - find the latest catalog leaf for each package ID and version and put it in Table Storage
- `FindPackageAssets` - find all assets recognized by NuGet restore and put them in big CSVs
- `FindPackageAssemblies` - find all information like public key token for all assemblies on NuGet.org

[src/ExplorePackages.Worker.Logic/CatalogScanDrivers](src/ExplorePackages.Worker.Logic/CatalogScanDrivers)

### Slow, serial queries (deprecated)

To support my repository metadata proposal ([NuGet/NuGetGallery#4941](https://github.com/NuGet/NuGetGallery/issues/4941))
I wrote a query to find all packages on NuGet.org that have the `<repository>` element in there .nuspec:

```csharp
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class FindRepositoriesNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindRepositoriesNuspecQuery;
        public string CursorName => CursorNames.FindRepositoriesNuspecQuery;

        public bool IsMatch(XDocument nuspec)
        {
            var repositoryEl = NuspecUtility.GetRepository(nuspec);

            return repositoryEl != null;
        }
    }
}
```

These are all of the queries based on a slow, in-process parallelization method. It just ran on my dev box and was not
very fast. It started running into scale issues for larger data sets.

[src/ExplorePackages.Entities.Logic/PackageQueries](src/ExplorePackages.Entities.Logic/PackageQueries)

### Consistency

I've also used this framework to look for inconsistencies in the V2 and V3 endpoints. To make this easier, I made a
little website to see if your package is fully propagated on NuGet.org (that is, the indexing is complete).

https://explorepackages.azurewebsites.net/

## TODO

I need to keep a TODO for this project since I indulge myself in all of the rabbit trails that I want and often lose
track of what I was originally working on. Maybe I should use issues for this? Too much work.

- Add a driver to write all catalog leaf items to CSV (very simple, helps with analysis)

- Chain `FindLatestLeaves` to the other catalog scan logic, to dedupe catalog leaves

- Remove the "NuGetOrgMin" once there is deduping via `FindLatestLeaves`

- Make `FindPackageAssemblies` use MiniZip to fetch the file listing first similar to `FinsPackageAssets`, ensuring that
  there is at least one assembly (by file extension) before downloading the full package.

- Store the MiniZip file listing in a ExplorePackages-maintained record (probably table storage) to avoid unnecessary
  hits to NuGet.org.
