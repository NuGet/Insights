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
| Driver                       | [`CatalogDataToCsv`](../drivers/CatalogDataToCsv.md)                                                                                                                                    |
| Record type                  | [`CatalogLeafItemRecord`](../../src/Worker.Logic/CatalogScan/Drivers/CatalogDataToCsv/CatalogLeafItemRecord.cs)                                                                         |

## Table schema

| Column name           | Data type        | Required                | Description                                                                             |
| --------------------- | ---------------- | ----------------------- | --------------------------------------------------------------------------------------- |
| CommitId              | string           | Yes                     | A unique identifier for the batch of items written to the catalog                       |
| CommitTimestamp       | timestamp        | Yes                     | When the item was written to the catalog                                                |
| LowerId               | string           | Yes                     | Lowercase package ID. Good for joins                                                    |
| Identity              | string           | Yes                     | Lowercase package ID and lowercase, normalized version. Good for joins                  |
| Id                    | string           | Yes                     | Original case package ID                                                                |
| Version               | string           | Yes                     | Original case, normalized package version                                               |
| Type                  | enum             | Yes                     | The type of catalog leaf item                                                           |
| Url                   | string           | Yes                     | The URL to the full leaf JSON document                                                  |
| PageUrl               | string           | Yes                     | The URL to the page containing the leaf item                                            |
| Published             | timestamp        | Yes                     | The published timestamp, this is widely used except for `IsListed` calculation          |
| IsListed              | bool             | Yes, for PackageDetails | Whether or not the package is marked as listed in this leaf                             |
| Created               | timestamp        | Yes, for PackageDetails | The created date of the package                                                         |
| LastEdited            | timestamp        | No                      | The last edited date of the package, changes with unlisted, listed, and many other ways |
| PackageSize           | long             | Yes, for PackageDetails | The package size in bytes                                                               |
| PackageHash           | string           | Yes, for PackageDetails | The base64 encoded package hash                                                         |
| PackageHashAlgorithm  | string           | Yes, for PackageDetails | The package hash algorithm name (e.g. `SHA512`)                                         |
| Deprecation           | object           | No                      | The deprecation JSON                                                                    |
| Vulnerabilities       | array of objects | No                      | The vulnerabilities list                                                                |
| HasRepositoryProperty | bool             | Yes, for PackageDetails | Whether or not there is a `repository` property in the JSON                             |
| PackageEntryCount     | int              | No                      | The number of items in the `packageEntries` JSON array                                  |
| NuspecPackageEntry    | object           | No                      | The ZIP entry metadata for the package .nuspec                                          |
| SignaturePackageEntry | object           | No                      | The ZIP entry metadata for the package signature `.signature.p7s`                       |

## Type schema

The Type enum indicates the possible variants of records.

See the [catalog API document](https://docs.microsoft.com/en-us/nuget/api/catalog-resource#item-types) for more information.

| Enum value     | Description                                                           |
| -------------- | --------------------------------------------------------------------- |
| PackageDelete  | Minimal deletion metadata at a moment when the package is deleted     |
| PackageDetails | The latest package metadata at a moment when the package is available |

## Deprecation schema

The schema of this object matches the package [deprecation object](https://learn.microsoft.com/en-us/nuget/api/registration-base-url-resource#package-deprecation) in the NuGet V3 protocol.

## Vulnerabilities schema

The schema of each object in the array matches the package [vulnerability object](https://learn.microsoft.com/en-us/nuget/api/registration-base-url-resource#vulnerabilities) in the NuGet V3 protocol.

## NuspecPackageEntry schema

The schema of the object is a subject of the properties available in the original JSON.

| Property name    | Data type | Required | Description                                                      |
| ---------------- | --------- | -------- | ---------------------------------------------------------------- |
| compressedLength | long      | true     | The compressed length in bytes of the ZIP file entry             |
| fullName         | string    | true     | The relative file path within the ZIP archive                    |
| length           | long      | true     | The declared, uncompressed length in bytes of the ZIP file entry |

## SignaturePackageEntry schema

This schema is the same as the NuspecPackageEntry property mentioned above.
