# PackageOwners

This table contains the set of current owners for the package ID. This table contains no historical data.

The owners are represented by their username. A package owner can either be an individual user or an organization.

|                              |                                                                                                    |
| ---------------------------- | -------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per unique package ID on NuGet.org, even if the package has zero owners                |
| Child tables                 |                                                                                                    |
| Parent tables                |                                                                                                    |
| Column used for partitioning | LowerId                                                                                            |
| Data file container name     | packageowners                                                                                      |
| Driver                       | [`OwnersToCsvUpdater`](../../src/Worker.Logic/MessageProcessors/OwnersToCsv/OwnersToCsvUpdater.cs) |
| Record type                  | [`PackageOwnerRecord`](../../src/Worker.Logic/MessageProcessors/OwnersToCsv/PackageOwnerRecord.cs) |

## Table schema

| Column name   | Data type        | Required | Description                                               |
| ------------- | ---------------- | -------- | --------------------------------------------------------- |
| AsOfTimestamp | timestamp        | No       | Unused, always empty                                      |
| LowerId       | string           | Yes      | Lowercase package ID. Good for joins                      |
| Id            | string           | Yes      | Original case package ID for an arbitrary package version |
| Owners        | array of strings | Yes      | Zero or more package owners                               |

## Owners schema

The Owners column is an array of strings where each string is either the username of a user or an organization on
NuGet.org. This array can be empty, meaning the package has no owners.
