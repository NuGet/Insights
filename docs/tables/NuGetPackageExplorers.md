# NuGetPackageExplorers

This table the result of the [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer)
[`SymbolValidator`](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer/blob/main/Core/SymbolValidation/SymbolValidator.cs) class
and contains package-level summary information. The purpose of this table is to assess the reproducibility of packages on NuGet.org.

|                              |                                                                                                                                              |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org                                                                                                         |
| Child tables                 | [NuGetPackageExplorerFiles](NuGetPackageExplorerFiles.md) joined on Identity                                                                 |
| Parent tables                |                                                                                                                                              |
| Column used for partitioning | Identity                                                                                                                                     |
| Data file container name     | nugetpackageexplorer                                                                                                                         |
| Driver implementation        | [`NuGetPackageExplorerToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/NuGetPackageExplorerToCsv/NuGetPackageExplorerToCsvDriver.cs) |
| Record type                  | [`NuGetPackageExplorerRecord`](../../src/Worker.Logic/CatalogScan/Drivers/NuGetPackageExplorerToCsv/NuGetPackageExplorerRecord.cs)           |

## Table schema

| Column name            | Data type | Required           | Description                                                                                                                      |
| ---------------------- | --------- | ------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| ScanId                 | string    | No                 | Unused, always empty                                                                                                             |
| ScanTimestamp          | timestamp | No                 | Unused, always empty                                                                                                             |
| LowerId                | string    | Yes                | Lowercase package ID. Good for joins                                                                                             |
| Identity               | string    | Yes                | Lowercase package ID and lowercase, normalized version. Good for joins                                                           |
| Id                     | string    | Yes                | Original case package ID                                                                                                         |
| Version                | string    | Yes                | Original case, normalized package version                                                                                        |
| CatalogCommitTimestamp | timestamp | Yes                | Latest catalog commit timestamp for the package                                                                                  |
| Created                | timestamp | Yes, for Available | When the package version was created                                                                                             |
| ResultType             | enum      | Yes                | Type of record (e.g. Available, Deleted)                                                                                         |
| SourceLinkResult       | enum      | Yes, for Available | Result of validating the symbols and [SourceLink](https://github.com/dotnet/sourcelink) information for the whole package        |
| DeterministicResult    | enum      | Yes, for Available | Result of validating whether the assemblies are built with deterministic settings                                                |
| HasCompilerFlagsResult | enum      | Yes, for Available | Result of validating compiler flags related to reproducibility                                                                   |
| IsSignedByAuthor       | bool      | Yes, for Available | Whether or not the package has an [author signature](https://docs.microsoft.com/en-us/nuget/reference/signed-packages-reference) |

## ResultType schema

The ResultType enum indicates the possible variants of records.

| Enum value        | Description                                                                       |
| ----------------- | --------------------------------------------------------------------------------- |
| Available         | The package is available and processed successfully                               |
| Deleted           | The package is deleted and no metadata is available                               |
| InvalidMetadata   | The package has unexpected metadata, so no results are available                  |
| NothingToValidate | The package has no assemblies in validated locations, so no results are available |
| Timeout           | The package validation timed out, so no results are available                     |

## SourceLinkResult schema

The SourceLinkResult enum has the following values.

| Enum value          | Description                                                |
| ------------------- | ---------------------------------------------------------- |
| HasUntrackedSources | The package has valid SourceLink but has untracked sources |
| InvalidSourceLink   | The package has invalid SourceLink metadata                |
| NoSourceLink        | The package has missing SourceLink metadata                |
| NoSymbols           | The package has missing symbols                            |
| NothingToValidate   | The package has no assemblies to validate                  |
| Valid               | The package has valid embedded symbols                     |
| ValidExternal       | The package has valid externally hosted symbols            |

## DeterministicResult schema

The DeterministicResult enum has the following values.

| Enum value          | Description                                                            |
| ------------------- | ---------------------------------------------------------------------- |
| HasUntrackedSources | The package has valid deterministic settings but has untracked sources |
| NonDeterministic    | The package has non-deterministic settings                             |
| NothingToValidate   | The package has no assemblies to validate                              |
| Valid               | The package has deterministic assemblies and sources                   |

## HasCompilerFlagsResult schema

The HasCompilerFlagsResult enum has the following values.

| Enum value        | Description                                                                |
| ----------------- | -------------------------------------------------------------------------- |
| Missing           | The package symbols don't have compiler flags                              |
| NothingToValidate | The package has no assemblies to validate                                  |
| Present           | The package symbols have compiler flags but are too old to be reproducible |
| Valid             | The package symbols have compiler flags and are reproducible               |
