# ExplorePackages

Explore packages on a V3 NuGet package source that has a catalog (NuGet.org!).

## Purpose

The purpose of this repository is to explore oddities and inconsistencies on NuGet.org's available packages. To support
this goal, I've built several generic ways of writing "queries" which search for packages with arbitrary characteristics.

### Parallelized queries on Azure Functions

I've built several "drivers" that implement what to do for each unit of work. A unit of work is represented by a queue
message that Azure Functions is triggered on. The unit of work can be based on a catalog index, catalog page, or catalog
leaf.

Results are stored in different ways but so far it's either results in Azure Table Storage (super cheap and scalable) or
Azure Blob Storage CSV files (easy import to Kusto a.k.a Azure Data Explorer).

- [`FindPackageAssemblies`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindPackageAssemblies/FindPackageAssembliesDriver.cs) - find information like public key tokens for all assemblies on NuGet.org
- [`FindPackageAssets`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindPackageAssets/FindPackageAssetsDriver.cs) - find all assets recognized by NuGet restore
- [`FindLatestLeaves`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindLatestLeaves/FindLatestLeavesDriver.cs) - find the latest catalog leaf for each package ID and version
- [`FindCatalogLeafItems`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindCatalogLeafItems/FindCatalogLeafItemsDriver.cs) - write all catalog leaf items to big CSVs for analysis

#### Performance and cost

Tested timestamp ranges:
- From the beginning of the catalog:
  - Min: `2015-02-01T06:22:45.8488496Z`
  - Max: `2020-12-27T08:10:52.8300258Z`
  - Page count: 11,621
  - Leaf count: 6,339,112
  - Unique packages: 3,597,830
  - Drivers that used this range:
    - `FindLatestLeaves`
    - `FindCatalogLeafItems`
- Mininum commit to get all **non-deleted** packages:
  - Min: `2018-08-08T16:29:16.4488297Z`
  - Max: `2020-12-27T08:10:52.8300258Z`
  - Page count: 7,435
  - Leaf count: 4,007,407
  - Unique packages: 3,593,656
  - Tests that used this range:
    - `FindPackageAssemblies`
    - `FindPackageAssets`
    - `FindLatestLeaves`

Results:
- `FindPackageAssemblies`
   - Runtime: 6 hours, 20 minutes, 8 seconds
   - Cost:
       - Functions: $8.73
       - Storage: $2.83
- `FindPackageAssets`
   - Runtime: 48 minutes, 23 seconds
   - Cost: 
       - Functions: $0.96
       - Storage: $0.21
- `FindPackageAssets` with latest leaf de-duping
   - Runtime: 42 minutes, 45 seconds
   - Cost: 
       - Functions: $1.49
       - Storage: $0.29
- `FindLatestLeaves` from "available min"
   - Runtime: 7 minutes, 56 seconds
   - Cost: <$0.01
- `FindLatestLeaves` from "absolute min"
   - Runtime: 8 minutes, 25 seconds
   - Cost: $0.01
- `FindCatalogLeafItems`
   - Runtime: 2 minutes, 28 seconds
   - Cost: <$0.01

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

- Chain `FindLatestLeaves` to the other catalog scan logic, to dedupe catalog leaves

- Remove the "NuGetOrgMin" once there is deduping via `FindLatestLeaves`

- Make `FindPackageAssemblies` use MiniZip to fetch the file listing first similar to `FinsPackageAssets`, ensuring that
  there is at least one assembly (by file extension) before downloading the full package.

- Store the MiniZip file listing in a ExplorePackages-maintained record (probably table storage) to avoid unnecessary
  hits to NuGet.org.

- Build a catalog index reader that uses range requests for perf

- Add the csv-spectrum tests to NCsvPerf

- Add a parameter to TablePrefixScanner to allow reading more than one page for a prefix query. Maybe there is some
  sweet spot greater than 1...

- Enable heterogeneous enqueueing and batch enqueue messages

- Enable enqueuing as many messages as possible for a bulk enqueue message, but no more and return the extra

- gzip batch or bulk enqueue messages?

- consider removing polling at the catalog page scan level, and poll for leaves at the index level

- enhance latest package leaves code to allow custom record shape, so it can write catalog leaf scan items directly
