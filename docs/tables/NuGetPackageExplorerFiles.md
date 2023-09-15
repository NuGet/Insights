# NuGetPackageExplorerFiles

This table the result of the [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer)
[`SymbolValidator`](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer/blob/main/Core/SymbolValidation/SymbolValidator.cs) class
and contains file-level information. The purpose of this table is to assess the reproducibility of packages on NuGet.org.

|                              |                                                                                                                                |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Cardinality                  | One or more rows per package, more than one if the package has multiple recognized assemblies                                  |
| Child tables                 |                                                                                                                                |
| Parent tables                | [NuGetPackageExplorers](NuGetPackageExplorers.md) joined on Identity                                                           |
| Column used for partitioning | Identity                                                                                                                       |
| Data file container name     | nugetpackageexplorerfiles                                                                                                      |
| Driver                       | [`NuGetPackageExplorerToCsv`](../drivers/NuGetPackageExplorerToCsv.md)                                                         |
| Record type                  | [`NuGetPackageExplorerFile`](../../src/Worker.Logic/CatalogScan/Drivers/NuGetPackageExplorerToCsv/NuGetPackageExplorerFile.cs) |

## Table schema

| Column name            | Data type        | Required           | Description                                                                    |
| ---------------------- | ---------------- | ------------------ | ------------------------------------------------------------------------------ |
| ScanId                 | string           | No                 | Unused, always empty                                                           |
| ScanTimestamp          | timestamp        | No                 | Unused, always empty                                                           |
| LowerId                | string           | Yes                | Lowercase package ID. Good for joins                                           |
| Identity               | string           | Yes                | Lowercase package ID and lowercase, normalized version. Good for joins         |
| Id                     | string           | Yes                | Original case package ID                                                       |
| Version                | string           | Yes                | Original case, normalized package version                                      |
| CatalogCommitTimestamp | timestamp        | Yes                | Latest catalog commit timestamp for the package                                |
| Created                | timestamp        | Yes, for Available | When the package version was created                                           |
| ResultType             | enum             | Yes                | Type of record (e.g. Available, Deleted)                                       |
| Path                   | string           | Yes, for Available | The path within the package to the validated file                              |
| Extension              | string           | Yes, for Available | The file extension from the path, including the dot, e.g. `.dll`               |
| HasCompilerFlags       | bool             | No                 | Whether or not the file has compiler flags                                     |
| HasSourceLink          | bool             | No                 | Whether or not the file has [SourceLink](https://github.com/dotnet/sourcelink) |
| HasDebugInfo           | bool             | No                 | Whether or not the file has debug info                                         |
| CompilerFlags          | object           | No                 | The compiler flags property bag                                                |
| SourceUrlRepoInfo      | array of objects | No                 | Summary info about SourceLink source URLs                                      |
| PdbType                | enum             | No                 | The type of PDBs for the assembly                                              |

## ResultType schema

The ResultType enum indicates the possible variants of records.

| Enum value        | Description                                                                       |
| ----------------- | --------------------------------------------------------------------------------- |
| Available         | The package is available and processed successfully                               |
| Deleted           | The package is deleted and no metadata is available                               |
| InvalidMetadata   | The package has unexpected metadata, so no results are available                  |
| NothingToValidate | The package has no assemblies in validated locations, so no results are available |
| Timeout           | The package validation timed out, so no results are available                     |

## CompilerFlags schema

The CompilerFlags object is an loose property bag of custom compiler flag metadata baked into the PDB.

## SourceUrlRepoInfo schema

The ReferenceGroups field is an array of objects. Each object has the following schema.

| Property name | Data type | Required | Description                                                |
| ------------- | --------- | -------- | ---------------------------------------------------------- |
| Repo          | object    | true     | Info about the source repo                                 |
| FileCount     | int       | true     | The number of source files with this same source repo info |
| Example       | string    | true     | An example source URL                                      |

Each Repo object has the following base schema.

| Property name | Data type | Required | Description             |
| ------------- | --------- | -------- | ----------------------- |
| Type          | enum      | true     | The type of source repo |

The Repo Type enum has the following values.

| Enum value | Description                                                     |
| ---------- | --------------------------------------------------------------- |
| GitHub     | The source URL is hosted on GitHub.com                          |
| Invalid    | The source URL is invalid                                       |
| Unknown    | The source URL is a valid URL but does not have a known pattern |

When the Repo Type property is Unknown, the Repo object has the following additional properties.

| Property name | Data type | Required | Description       |
| ------------- | --------- | -------- | ----------------- |
| Host          | string    | true     | The URL host name |

When the Repo Type property is Invalid, there are no additional properties.

When the Repo Type property is GitHub, the Repo object has the following additional properties.

| Property name | Data type | Required | Description                                                 |
| ------------- | --------- | -------- | ----------------------------------------------------------- |
| Owner         | string    | true     | The GitHub owner (individual username or organization name) |
| Repo          | string    | true     | The repository name                                         |
| Ref           | string    | true     | The Git ref name, typically the commit hash                 |

## PdbType schema

The PdbType enum has the following values.

| Enum value | Description                                                                                                             |
| ---------- | ----------------------------------------------------------------------------------------------------------------------- |
| Embedded   | The symbol file is embedded in the assembly                                                                             |
| Full       | The symbol file is a full (traditional) PDB                                                                             |
| Portable   | The symbol file is a [portable PDB](https://github.com/dotnet/core/blob/main/Documentation/diagnostics/portable_pdb.md) |
