# VerifiedPackages

This table contains the verified status for each package ID.

A verified package ID is one that exists in a reserved namespace and is owned by one of the owners of the reserved
namespace. For more information about package ID prefix reservation, see the
[NuGet documentation](https://docs.microsoft.com/en-us/nuget/nuget-org/id-prefix-reservation).

A verified package is one that has the blue checkmark icon on nuget.org and Visual Studio package management UI.

|                              |                                                                                                                                  |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per unique package ID on NuGet.org.                                                                                  |
| Child tables                 |                                                                                                                                  |
| Parent tables                |                                                                                                                                  |
| Column used for partitioning | LowerId                                                                                                                          |
| Data file container name     | verifiedpackages                                                                                                                 |
| Driver                       | [`VerifiedPackagesToCsvUpdater`](../../src/Worker.Logic/MessageProcessors/VerifiedPackagesToCsv/VerifiedPackagesToCsvUpdater.cs) |
| Record type                  | [`VerifiedPackageRecord`](../../src/Worker.Logic/MessageProcessors/VerifiedPackagesToCsv/VerifiedPackageRecord.cs)               |

## Table schema

| Column name   | Data type | Required | Description                                               |
| ------------- | --------- | -------- | --------------------------------------------------------- |
| AsOfTimestamp | timestamp | No       | Unused, always empty                                      |
| LowerId       | string    | Yes      | Lowercase package ID. Good for joins                      |
| Id            | string    | Yes      | Original case package ID for an arbitrary package version |
| IsVerified    | bool      | Yes      | Whether or not the package ID is verified                 |

