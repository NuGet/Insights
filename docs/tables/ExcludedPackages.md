# ExcludedPackages

This table contains the whether each package ID is included in the default NuGet.org search results. The default search
results can be found at https://www.nuget.org/packages or when you open the Browse tab in the NuGet package manager UI
in Visual Studio.

A package would be excluded from the default search result likely because it is part of the .NET base class library
(i.e. a base framework) and is therefore not interesting for someone who would be looking for additional (as in not
included by default) libraries to use in their project.

|                              |                                                                                                                                  |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per unique package ID on NuGet.org.                                                                                  |
| Child tables                 |                                                                                                                                  |
| Parent tables                |                                                                                                                                  |
| Column used for partitioning | LowerId                                                                                                                          |
| Data file container name     | excludedpackages                                                                                                                 |
| Driver                       | [`ExcludedPackagesToCsvUpdater`](../../src/Worker.Logic/MessageProcessors/ExcludedPackagesToCsv/ExcludedPackagesToCsvUpdater.cs) |
| Record type                  | [`ExcludedPackageRecord`](../../src/Worker.Logic/MessageProcessors/ExcludedPackagesToCsv/ExcludedPackageRecord.cs)               |

## Table schema

| Column name   | Data type | Required | Description                                                 |
| ------------- | --------- | -------- | ----------------------------------------------------------- |
| AsOfTimestamp | timestamp | No       | Unused, always empty                                        |
| LowerId       | string    | Yes      | Lowercase package ID. Good for joins                        |
| Id            | string    | Yes      | Original case package ID for an arbitrary package version   |
| IsExcluded    | bool      | Yes      | Whether the package is excluded from default search results |

