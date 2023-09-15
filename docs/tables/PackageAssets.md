# PackageAssets

This table contains the package assets within each .nupkg on NuGet.org. A package asset is a file that can be used for
a specific purpose according to NuGet's [restore command](https://docs.microsoft.com/en-us/nuget/consume-packages/package-restore).
If a file within a NuGet package follows certain conventions, it can be recognized by NuGet restore and is usable by
package consumers.

|                              |                                                                                                |
| ---------------------------- | ---------------------------------------------------------------------------------------------- |
| Cardinality                  | One or more rows per package, more than one if the package has multiple recognized assets      |
| Child tables                 |                                                                                                |
| Parent tables                |                                                                                                |
| Column used for partitioning | Identity                                                                                       |
| Data file container name     | packageassets                                                                                  |
| Driver                       | [`PackageAssetToCsv`](../drivers/PackageAssetToCsv.md)                                         |
| Record type                  | [`PackageAsset`](../../src/Worker.Logic/CatalogScan/Drivers/PackageAssetToCsv/PackageAsset.cs) |

## Table schema

| Column name                     | Data type | Required                                     | Description                                                                                                                                                                                 |
| ------------------------------- | --------- | -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ScanId                          | string    | No                                           | Unused, always empty                                                                                                                                                                        |
| ScanTimestamp                   | timestamp | No                                           | Unused, always empty                                                                                                                                                                        |
| LowerId                         | string    | Yes                                          | Lowercase package ID. Good for joins                                                                                                                                                        |
| Identity                        | string    | Yes                                          | Lowercase package ID and lowercase, normalized version. Good for joins                                                                                                                      |
| Id                              | string    | Yes                                          | Original case package ID                                                                                                                                                                    |
| Version                         | string    | Yes                                          | Original case, normalized package version                                                                                                                                                   |
| CatalogCommitTimestamp          | timestamp | Yes                                          | Latest catalog commit timestamp for the package                                                                                                                                             |
| Created                         | timestamp | Yes, for non-Deleted                         | When the package version was created                                                                                                                                                        |
| ResultType                      | enum      | Yes                                          | Type of record (e.g. AvailableAssets, Deleted)                                                                                                                                              |
| PatternSet                      | enum      | Yes, for AvailableAssets                     | Which [ManagedCodeConventions](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/ContentModel/ManagedCodeConventions.cs) pattern sets this asset was matched by |
| PropertyAnyValue                | string    | No                                           | Always empty                                                                                                                                                                                |
| PropertyCodeLanguage            | string    | Yes, for ContentFiles                        | The code language found in the file path                                                                                                                                                    |
| PropertyTargetFrameworkMoniker  | string    | Yes, for AvailableAssets                     | The TFM (target framework moniker) found in the file path or a default                                                                                                                      |
| PropertyLocale                  | string    | No                                           | Always empty                                                                                                                                                                                |
| PropertyManagedAssembly         | string    | No                                           | Always empty                                                                                                                                                                                |
| PropertyMSBuild                 | string    | No                                           | Always empty                                                                                                                                                                                |
| PropertyRuntimeIdentifier       | string    | Yes, for NativeLibraries and ToolsAssemblies | The RID (runtime identifier) found in the file path                                                                                                                                         |
| PropertySatelliteAssembly       | string    | No                                           | Always empty                                                                                                                                                                                |
| Path                            | string    | Yes, for AvailableAssets                     | Always empty                                                                                                                                                                                |
| FileName                        | string    | Yes, for AvailableAssets                     | The file name from the Path                                                                                                                                                                 |
| FileExtension                   | string    | Yes, for AvailableAssets                     | The file extension from the Path                                                                                                                                                            |
| TopLevelFolder                  | string    | Yes, for AvailableAssets                     | The first folder (i.e. directory) name from the Path                                                                                                                                        |
| RoundTripTargetFrameworkMoniker | string    | Yes, for AvailableAssets                     | PropertyTargetFrameworkMoniker parsed and normalized                                                                                                                                        |
| FrameworkName                   | string    | Yes, for AvailableAssets                     | The framework name component of PropertyTargetFrameworkMoniker                                                                                                                              |
| FrameworkVersion                | string    | Yes, for AvailableAssets                     | The framework version component of PropertyTargetFrameworkMoniker, defaults to `0.0.0.0`                                                                                                    |
| FrameworkProfile                | string    | No                                           | The framework profile component of PropertyTargetFrameworkMoniker                                                                                                                           |
| PlatformName                    | string    | No                                           | The platform name component of PropertyTargetFrameworkMoniker                                                                                                                               |
| PlatformVersion                 | string    | Yes, for AvailableAssets                     | The platform version component of PropertyTargetFrameworkMoniker, defaults to `0.0.0.0`                                                                                                     |

## ResultType schema

The ResultType enum indicates the possible variants of records.

| Enum value      | Description                                            |
| --------------- | ------------------------------------------------------ |
| AvailableAssets | The package has one more more recognized assets        |
| Deleted         | The package is deleted and therefore has no assets     |
| Error           | There was a known error while processing assets        |
| NoAssets        | The package is available but has not recognized assets |

There are two error cases that could lead to the Error ResultType. Both error cases come down to an invalid portable profile leading to a `FrameworkException` or `ArgumentException` while finding assets.

## PatternSet schema

The PatternSet enum has the following values. Comments are copied from NuGet.Client's [ManagedCodeConventions](https://github.com/NuGet/NuGet.Client/blob/9f2da3906bf40ebcb9a6692a579b1a554ce31736/src/NuGet.Core/NuGet.Packaging/ContentModel/ManagedCodeConventions.cs).

| Enum value                 | Description                                                                                           |
| -------------------------- | ----------------------------------------------------------------------------------------------------- |
| CompileLibAssemblies       | Pattern used to locate lib assemblies for compile.                                                    |
| CompileRefAssemblies       | Pattern used to locate ref assemblies for compile.                                                    |
| ContentFiles               | Pattern used to identify content files                                                                |
| EmbedAssemblies            | Pattern used to locate embed interop types assemblies                                                 |
| MSBuildFiles               | Pattern used to identify MSBuild targets and props files                                              |
| MSBuildMultiTargetingFiles | Pattern used to identify MSBuild global targets and props files                                       |
| MSBuildTransitiveFiles     | Pattern used to identify MSBuild transitive targets and props files                                   |
| NativeLibraries            | Pattern used to locate all files designed for loading as native code libraries at run-time            |
| ResourceAssemblies         | Pattern used to locate all files designed for loading as managed code resource assemblies at run-time |
| RuntimeAssemblies          | Pattern used to locate all files designed for loading as managed code assemblies at run-time          |
| ToolsAssemblies            | Pattern used to identify Tools assets for global tools                                                |
