# PackageIcons

This table contains metadata about package icons available on NuGet.org. This contains information about both embedded
icons and icon URL icons downloaded to NuGet.org. The processing of images is down using [`System.Drawing.Bitmap`](https://docs.microsoft.com/en-us/dotnet/api/system.drawing.bitmap).

|                              |                                                                                                                   |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org                                                                              |
| Child tables                 |                                                                                                                   |
| Parent tables                |                                                                                                                   |
| Column used for partitioning | Identity                                                                                                          |
| Data file container name     | packageicons                                                                                                      |
| Driver implementation        | [`PackageIconToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageIconToCsv/PackageIconToCsvDriver.cs) |
| Record type                  | [`PackageIcon`](../../src/Worker.Logic/CatalogScan/Drivers/PackageIconToCsv/PackageIcon.cs)                       |

## Table schema

| Column name            | Data type        | Required             | Description                                                                                                                           |
| ---------------------- | ---------------- | -------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| ScanId                 | string           | No                   | Unused, always empty                                                                                                                  |
| ScanTimestamp          | timestamp        | No                   | Unused, always empty                                                                                                                  |
| LowerId                | string           | Yes                  | Lowercase package ID. Good for joins                                                                                                  |
| Identity               | string           | Yes                  | Lowercase package ID and lowercase, normalized version. Good for joins                                                                |
| Id                     | string           | Yes                  | Original case package ID                                                                                                              |
| Version                | string           | Yes                  | Original case, normalized package version                                                                                             |
| CatalogCommitTimestamp | timestamp        | Yes                  | Latest catalog commit timestamp for the package                                                                                       |
| Created                | timestamp        | Yes, for non-Deleted | When the package version was created                                                                                                  |
| ResultType             | enum             | Yes                  | Type of record (e.g. Available, Deleted)                                                                                              |
| FileSize               | long             | Yes, for existing    | Size of the image file in bytes                                                                                                       |
| MD5                    | string           | Yes, for existing    | MD5 hash of the image bytes                                                                                                           |
| SHA1                   | string           | Yes, for existing    | SHA1 hash of the image bytes                                                                                                          |
| SHA256                 | string           | Yes, for existing    | SHA256 hash of the image bytes                                                                                                        |
| SHA512                 | string           | Yes, for existing    | SHA512 hash of the image bytes                                                                                                        |
| ContentType            | string           | No                   | Content-Type response header on the image file, if present                                                                            |
| Format                 | string           | Yes, for Available   | String representing image format, e.g. "Png" or "Gif"                                                                                 |
| Width                  | int              | Yes, for Available   | Width of the image in pixels                                                                                                          |
| Height                 | int              | Yes, for Available   | Height of the image in pixels                                                                                                         |
| FrameCountByTime       | int              | No                   | Number of frames in an animated image                                                                                                 |
| FrameCountByResolution | int              | No                   | Number of frames in an image with multiple resolutions (questionable correctness)                                                     |
| FrameCountByPage       | int              | No                   | Number of frames, per [`FrameDimension.Page`](https://docs.microsoft.com/en-us/dotnet/api/system.drawing.imaging.framedimension.page) |
| HorizontalResolution   | float            | Yes, for Available   | Horizontal resolution in pixels per inch                                                                                              |
| VerticalResolution     | float            | Yes, for Available   | Vertical resolution in pixels per inch                                                                                                |
| Flags                  | int              | Yes, for Available   | [`ImageFlags`](https://docs.microsoft.com/en-us/dotnet/api/system.drawing.imaging.imageflags) in `int` form                           |
| PixelFormat            | string           | Yes, for Available   | [`PixelFormat`](https://docs.microsoft.com/en-us/dotnet/api/system.drawing.imaging.pixelformat) in `string` form                      |
| PropertyItems          | array of objects | Yes, for Available   | Type information about image metadata                                                                                                 |

In the Required column of the table above, "existing" refers to Available or Error results. These are icons that can be downloaded.

## ResultType schema

| Enum value | Description                                                                |
| ---------- | -------------------------------------------------------------------------- |
| Available  | The icon is available and was opened successfully                          |
| Deleted    | The package is deleted and no metadata is available                        |
| Error      | The icon is available but could not be opened with `System.Drawing.Bitmap` |
| NoIcon     | The package exists but no icon is available                                |

## PropertyItems schema

The PropertyItems column is an array of objects. Each object is a projection of [`System.Drawing.Imaging.PropertyItem`](https://docs.microsoft.com/en-us/dotnet/api/system.drawing.imaging.propertyitem) and has the following schema.

| Property name | Data type | Required | Description                                                                                                                      |
| ------------- | --------- | -------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Id            | int       | true     | The integer form of [`PropertyItem.Id`](https://docs.microsoft.com/en-us/dotnet/api/system.drawing.imaging.propertyitem.id)      |
| Type          | short     | true     | The integer form of [`PropertyItem.TypeS`](https://docs.microsoft.com/en-us/dotnet/api/system.drawing.imaging.propertyitem.type) |
