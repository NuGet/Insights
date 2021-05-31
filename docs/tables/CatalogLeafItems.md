# CatalogLeafItems

This table contains metadata about each catalog leaf item in the NuGet.org [V3 catalog resource](https://docs.microsoft.com/en-us/nuget/api/catalog-resource).
A catalog leaf item is produced any time a package is created, unlisted, relisted, deprecated, deleted, or several other less common events.

Most packages have more than one catalog leaf item due to the NuGet.org
[repository signing effort](https://devblogs.microsoft.com/nuget/introducing-repository-signatures/) executed from late
2018 to early 2019. The catalog was established after many packages were accepted to the NuGet.org V1 and V2 APIs so
some packages were created before their first commit timestamp.

|                              |                                                                                                                                                                                         |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | One or more rows per package, more than one if the package has multiple [catalog leaf items](https://docs.microsoft.com/en-us/nuget/api/catalog-resource#catalog-item-object-in-a-page) |
| Child tables                 |                                                                                                                                                                                         |
| Parent tables                |                                                                                                                                                                                         |
| Column used for partitioning | PageUrl                                                                                                                                                                                 |
| Data file container name     | catalogleafitems                                                                                                                                                                        |
| Driver implementation        | [`CatalogLeafItemToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/CatalogLeafItemToCsv/CatalogLeafItemToCsvDriver.cs)                                                           |
| Record type                  | [`CatalogLeafItemRecord`](../../src/Worker.Logic/CatalogScan/Drivers/CatalogLeafItemToCsv/CatalogLeafItemRecord.cs)                                                                     |

## Table schema

| Column name     | Data type | Required | Description                                                            |
| --------------- | --------- | -------- | ---------------------------------------------------------------------- |
| CommitId        | string    | Yes      | A unique identifier for the batch of items written to the catalog      |
| CommitTimestamp | timestamp | Yes      | When the item was written to the catalog                               |
| LowerId         | string    | Yes      | Lowercase package ID. Good for joins                                   |
| Identity        | string    | Yes      | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id              | string    | Yes      | Original case package ID                                               |
| Version         | string    | Yes      | Original case, normalized package version                              |
| Type            | enum      | Yes      | The type of catalog leaf item                                          |
| Url             | string    | Yes      | The URL to the full leaf JSON document                                 |
| PageUrl         | string    | Yes      | The URL to the page containing the leaf item                           |

## Type schema

The Type enum indicates the possible variants of records.

See the [catalog API document](https://docs.microsoft.com/en-us/nuget/api/catalog-resource#item-types) for more information.

| Enum value     | Description                                                           |
| -------------- | --------------------------------------------------------------------- |
| PackageDelete  | Minimal deletion metadata at a moment when the package is deleted     |
| PackageDetails | The latest package metadata at a moment when the package is available |
