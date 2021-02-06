# ExplorePackages

Explore packages on a V3 NuGet package source that has a catalog (NuGet.org!).

## Purpose

The purpose of this repository is to explore oddities and inconsistencies on NuGet.org's available packages.

I've built several "drivers" that implement what to do for each unit of work. A unit of work is represented by a queue
message that Azure Functions is triggered on. The unit of work can be based on a catalog index, catalog page, or catalog
leaf.

Results are stored in different ways but so far it's either results in Azure Table Storage (super cheap and scalable) or
Azure Blob Storage CSV files (easy import to Kusto a.k.a Azure Data Explorer).

The main drivers for learning about pacakges are:

- [`FindPackageAssembly`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindPackageAssembly/FindPackageAssemblyDriver.cs) - find stuff like public key tokens for assemblie using `System.Reflection.Metadata`
- [`FindPackageAsset`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindPackageAsset/FindPackageAssetDriver.cs) - find assets recognized by NuGet restore
- [`FindPackageSignature`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindPackageSignature/FindPackageSignatureDriver.cs) - parse the NuGet package signature

Several other supporting drivers exist:

- [`FindPackageFile`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindPackageFile/FindPackageFileDriver.cs) - fetch information from the .nupkg and put it in Table Storage
- [`FindPackageManifest`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindPackageManifest/FindPackageManifestDriver.cs) - fetch the .nuspec and put it in Table Storage
- [`FindCatalogLeafItem`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindCatalogLeafItem/FindCatalogLeafItemDriver.cs) - write all catalog leaf items to big CSVs for analysis
- [`LatestLeaf`](src/ExplorePackages.Worker.Logic/CatalogScan/LatestLeaf/FindLatestLeafDriver.cs) - infrastructure to find the latest leaf per package ID and version
  - [`FindLatestPackageLeaf`](src/ExplorePackages.Worker.Logic/CatalogScan/Drivers/FindLatestPackageLeaf) - uses this generic infrastructure to write latest leaves to Table Storage

### Performance and cost

#### Results (February 2021)

Tested timestamp range:
- Min: `2015-02-01T06:22:45.8488496Z`
- Max: `2021-02-05T15:55:33.3608941Z`

Results:
- `FindPackageFile`
  - **Runtime: 37 minutes, 19 seconds**
  - **Total cost - $3.37**
  - Azure Functions cost - $2.77
    - bandwidth / data transfer out - $1.62
    - functions / execution time - $1.13
    - functions / total executions - $0.01
  - Azure Storage cost - $0.60
    - storage / tables / scan operations - $0.26
    - storage / tables / batch write operations - $0.15
    - storage / queues v2 / lrs class 1 operations - $0.13
    - storage / tiered block blob / all other operations - $0.01
    - storage / files / protocol operations - $0.01

#### Results (January 2021)

Tested timestamp ranges:
- From the beginning of the catalog:
  - Min: `2015-02-01T06:22:45.8488496Z`
  - Max: `2020-12-27T08:10:52.8300258Z`
  - Page count: 11,621
  - Leaf count: 6,339,112
  - Unique packages: 3,597,830
  - Drivers that used this range:
    - `FindLatestLeaf`
    - `FindCatalogLeafItem`
- Mininum commit to get all **non-deleted** packages:
  - Min: `2018-08-08T16:29:16.4488297Z`
  - Max: `2020-12-27T08:10:52.8300258Z`
  - Page count: 7,435
  - Leaf count: 4,007,407
  - Unique packages: 3,593,656
  - Tests that used this range:
    - `FindPackageAssembly`
    - `FindPackageAsset`
    - `FindLatestLeaf`

Results:
- `FindPackageAssembly`
   - Runtime: 4 hours, 38 minutes, 35 seconds
   - Cost:
       - Functions: $8.73
       - Storage: $2.83
- `FindPackageAsset`
   - Runtime: 48 minutes, 23 seconds
   - Cost: 
       - Functions: $0.96
       - Storage: $0.21
- `FindPackageAsset` with latest leaf de-duping
   - Runtime: 41 minutes, 38 seconds
   - Cost: 
       - Functions: $1.49
       - Storage: $0.29
- `FindLatestLeaf` from "available min"
   - Runtime: 7 minutes, 56 seconds
   - Cost: <$0.01
- `FindLatestLeaf` from "absolute min"
   - Runtime: 8 minutes, 25 seconds
   - Cost: $0.01
- `FindCatalogLeafItem`
   - Runtime: 2 minutes, 28 seconds
   - Cost: <$0.01

### Consistency

I've also used this framework to look for inconsistencies in the V2 and V3 endpoints. To make this easier, I made a
little website to see if your package is fully propagated on NuGet.org (that is, the indexing is complete).

https://explorepackages.azurewebsites.net/

