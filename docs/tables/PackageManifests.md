# PackageManifests

This table contains all well known fields in the NuGet package manifest (.nuspec file) that are recognized by official
NuGet client software.

The data in this table is meant to represent the original data discoverable in the .nuspec but depending on the
implementation of NuGet client's
[`NuspecReader`](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/NuspecReader.cs)
class, some fields may be manipulated or parsed projections of the original XML string.

|                              |                                                                                                                               |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org                                                                                          |
| Child tables                 |                                                                                                                               |
| Parent tables                |                                                                                                                               |
| Column used for partitioning | Identity                                                                                                                      |
| Data file container name     | packagemanifests                                                                                                              |
| Driver implementation        | [`PackageManifestToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageManifestToCsv/PackageManifestToCsvDriver.cs) |
| Record type                  | [`PackageManifestRecord`](../../src/Worker.Logic/CatalogScan/Drivers/PackageManifestToCsv/PackageManifestRecord.cs)           |

## Table schema

| Column name                    | Data type        | Required             | Description                                                                                                                                        |
| ------------------------------ | ---------------- | -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| ScanId                         | string           | No                   | Unused, always empty                                                                                                                               |
| ScanTimestamp                  | timestamp        | No                   | Unused, always empty                                                                                                                               |
| LowerId                        | string           | Yes                  | Lowercase package ID. Good for joins                                                                                                               |
| Identity                       | string           | Yes                  | Lowercase package ID and lowercase, normalized version. Good for joins                                                                             |
| Id                             | string           | Yes                  | Original case package ID                                                                                                                           |
| Version                        | string           | Yes                  | Original case, normalized package version                                                                                                          |
| CatalogCommitTimestamp         | timestamp        | Yes                  | Latest catalog commit timestamp for the package                                                                                                    |
| Created                        | timestamp        | Yes, for non-Deleted | When the package version was created                                                                                                               |
| ResultType                     | enum             | Yes                  | Type of record (e.g. Available, Deleted)                                                                                                           |
| Size                           | int              | Yes, for non-Deleted | Size of .nuspec in bytes                                                                                                                           |
| OriginalId                     | string           | Yes, for non-Deleted | Original package ID, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#id)                                                            |
| OriginalVersion                | string           | Yes, for non-Deleted | Original package version, non-normalized, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#version)                                  |
| MinClientVersion               | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#minclientversion)                                                                   |
| DevelopmentDependency          | bool             | Yes, for non-Deleted | Defaults to false, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#developmentdependency)                                           |
| IsServiceable                  | bool             | Yes, for non-Deleted | Defaults to false, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#serviceable)                                                     |
| Authors                        | string           | No                   | Freeform author names, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#authors)                                                     |
| Copyright                      | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#copyright)                                                                          |
| Description                    | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#description)                                                                        |
| Icon                           | string           | No                   | Embedded icon file name, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#icon)                                                      |
| IconUrl                        | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#iconurl)                                                                            |
| Language                       | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#language)                                                                           |
| LicenseUrl                     | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#licenseurl)                                                                         |
| Owners                         | string           | No                   | Freeform owner names, not the same as [NuGet.org owners](PackageOwners.md), [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#owners) |
| ProjectUrl                     | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#projecturl)                                                                         |
| Readme                         | string           | No                   | Embedded readme file name, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#readme)                                                  |
| ReleaseNotes                   | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#releasenotes)                                                                       |
| RequireLicenseAcceptance       | bool             | Yes, for non-Deleted | Defaults to false, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#requirelicenseacceptance)                                        |
| Summary                        | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#summary)                                                                            |
| Tags                           | string           | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#tags)                                                                               |
| Title                          | string           | No                   | Display name for package, not the same as package ID, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#title)                        |
| PackageTypes                   | array of objects | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#packagetypes)                                                                       |
| LicenseMetadata                | object           | No                   | Embedded license info, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#license)                                                     |
| RepositoryMetadata             | object           | No                   | Source repository info, [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#repository)                                                 |
| ReferenceGroups                | array of objects | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#references)                                                                         |
| ContentFiles                   | array of objects | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#contentfiles)                                                                       |
| DependencyGroups               | array of objects | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#dependencies)                                                                       |
| FrameworkAssemblyGroups        | array of objects | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#frameworkassemblies)                                                                |
| FrameworkRefGroups             | array of objects | No                   | [docs](https://docs.microsoft.com/en-us/nuget/reference/nuspec#framework-reference-groups)                                                         |
| ContentFilesHasFormatException | bool             | Yes, for non-Deleted | Bad `<file>` element caused an error, defaults to false                                                                                            |
| DependencyGroupsHasMissingId   | bool             | Yes, for non-Deleted | Missing ID in dependency groups caused an error, defaults to false                                                                                 |

## ResultType schema

The ResultType enum indicates the possible variants of records.

| Enum value | Description                                                               |
| ---------- | ------------------------------------------------------------------------- |
| Available  | The package is available and processed successfully                       |
| Deleted    | The package is deleted and no metadata is available                       |
| Error      | There was an error when processing the package leading to partial results |

If the ResultType is Error, the table field ContentFilesHasFormatException or DependencyGroupsHasMissingId will be true.

## PackageTypes schema

The PackageTypes field is an array of objects. Each object has the following schema.

| Property name | Data type | Required | Description                                             |
| ------------- | --------- | -------- | ------------------------------------------------------- |
| Name          | string    | true     | The name of the package type                            |
| Version       | string    | true     | The System.Version of the package type. Defaults to 0.0 |

## LicenseMetadata schema

The LicenseMetadata field is an object with the following schema.

| Property name     | Data type        | Required | Description                                                                                    |
| ----------------- | ---------------- | -------- | ---------------------------------------------------------------------------------------------- |
| Type              | enum             | true     | The type of license value                                                                      |
| License           | string           | true     | Either the SPDX license expression or license file name                                        |
| Version           | string           | true     | The version of the license element. Always `1.0.0`                                             |
| LicenseUrl        | string           | true     | Either a link to the parsed license expression or a deprecation URL for embedded license files |
| WarningsAndErrors | array of strings | false    | List of warnings and errors found file parsing the license expression                          |

The Type property is an enum with the following possible values.

| Enum value | Description                                         |
| ---------- | --------------------------------------------------- |
| Expression | The package has an embedded SPDX license expression |
| File       | The package has an embedded license file            |

## RepositoryMetadata schema

The RepositoryMetadata field is an object with the following schema.

| Property name | Data type | Required | Description                                           |
| ------------- | --------- | -------- | ----------------------------------------------------- |
| Type          | string    | false    | The type of source repository                         |
| Url           | string    | false    | The URL to the source repository                      |
| Branch        | string    | false    | The branch of the source                              |
| Commit        | string    | false    | The commit hash or revision identifier for the source |

## ReferenceGroups schema

The ReferenceGroups field is an array of objects. Each object has the following schema.

See the [.nuspec documentation](https://docs.microsoft.com/en-us/nuget/reference/nuspec#references) on the field for more information.

| Property name   | Data type        | Required | Description                                                       |
| --------------- | ---------------- | -------- | ----------------------------------------------------------------- |
| TargetFramework | string           | true     | Short target framework name for the group of references           |
| Items           | array of strings | true     | The assembly reference names in the single target framework group |

## ContentFiles schema

The ContentFiles field is an array of objects. Each object has the following schema.

See the [.nuspec documentation](https://docs.microsoft.com/en-us/nuget/reference/nuspec#contentfiles) for more information.

| Property name | Data type | Required |
| ------------- | --------- | -------- |
| Include       | string    | true     |
| Exclude       | string    | false    |
| BuildAction   | string    | false    |
| CopyToOutput  | bool      | false    |
| Flatten       | bool      | false    |

## DependencyGroups schema

The DependencyGroups field is an array of objects. Each object has the following schema.

See the [.nuspec documentation](https://docs.microsoft.com/en-us/nuget/reference/nuspec#dependencies) for more information.

| Property name   | Data type        | Required | Description                                                        |
| --------------- | ---------------- | -------- | ------------------------------------------------------------------ |
| TargetFramework | string           | true     | Short target framework name for the group of references            |
| Packages        | array of objects | true     | The framework reference names in the single target framework group |

The Packages property is an array of objects. Each object has the following schema.

| Property name | Data type        | Required | Description                                                |
| ------------- | ---------------- | -------- | ---------------------------------------------------------- |
| Id            | string           | true     | The package ID of the dependency                           |
| VersionRange  | string           | true     | The NuGet version range for acceptable dependency versions |
| Include       | array of strings | true     | Can be an empty array                                      |
| Exclude       | array of strings | true     | Can be an empty array                                      |

## FrameworkAssemblyGroups schema

The FrameworkAssemblyGroups field is an array of objects. Each object has the following schema.

See the [.nuspec documentation](https://docs.microsoft.com/en-us/nuget/reference/nuspec#frameworkassemblies) for more information.

| Property name   | Data type        | Required | Description                                                       |
| --------------- | ---------------- | -------- | ----------------------------------------------------------------- |
| TargetFramework | string           | true     | Short target framework name for the group of framework assemblies |
| Items           | array of strings | true     | The framework assembly names in the single group                  |

## FrameworkRefGroups schema

The FrameworkRefGroups field is an array of objects. Each object has the following schema.

See the [.nuspec documentation](https://docs.microsoft.com/en-us/nuget/reference/nuspec#framework-reference-groups) for more information.

| Property name       | Data type       | Required | Description                                                       |
| ------------------- | --------------- | -------- | ----------------------------------------------------------------- |
| TargetFramework     | string          | true     | Short target framework name for the group of framework references |
| FrameworkReferences | array of object | true     | The framework references in the single target framework group     |

The FrameworkReferences property is an array of objects. Each object has the following schema.

| Property name | Data type | Required | Description                         |
| ------------- | --------- | -------- | ----------------------------------- |
| Name          | string    | true     | The name of the framework reference |
