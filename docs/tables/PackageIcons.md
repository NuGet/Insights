# PackageIcons

This table contains metadata about package icons available on NuGet.org. This contains information about both embedded
icons and icon URL icons downloaded to NuGet.org. The processing of images is done using [Magick.NET](https://github.com/dlemstra/Magick.NET).

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

| Column name            | Data type        | Required             | Description                                                             |
| ---------------------- | ---------------- | -------------------- | ----------------------------------------------------------------------- |
| ScanId                 | string           | No                   | Unused, always empty                                                    |
| ScanTimestamp          | timestamp        | No                   | Unused, always empty                                                    |
| LowerId                | string           | Yes                  | Lowercase package ID. Good for joins                                    |
| Identity               | string           | Yes                  | Lowercase package ID and lowercase, normalized version. Good for joins  |
| Id                     | string           | Yes                  | Original case package ID                                                |
| Version                | string           | Yes                  | Original case, normalized package version                               |
| CatalogCommitTimestamp | timestamp        | Yes                  | Latest catalog commit timestamp for the package                         |
| Created                | timestamp        | Yes, for non-Deleted | When the package version was created                                    |
| ResultType             | enum             | Yes                  | Type of record (e.g. Available, Deleted)                                |
| FileSize               | long             | Yes, for existing    | Size of the image file in bytes                                         |
| MD5                    | string           | Yes, for existing    | MD5 hash of the image bytes                                             |
| SHA1                   | string           | Yes, for existing    | SHA1 hash of the image bytes                                            |
| SHA256                 | string           | Yes, for existing    | SHA256 hash of the image bytes                                          |
| SHA512                 | string           | Yes, for existing    | SHA512 hash of the image bytes                                          |
| ContentType            | string           | No                   | Content-Type response header on the image file, if present              |
| Format                 | string           | Yes, for Available   | String representing image format, e.g. "Png" or "Gif"                   |
| Width                  | int              | Yes, for Available   | Width of the image in pixels                                            |
| Height                 | int              | Yes, for Available   | Height of the image in pixels                                           |
| FrameCount             | int              | No                   | Number of frames in an animated image, or one with multiple resolutions |
| IsOpaque               | bool             | Yes, for Available   | Whether or not all pixels are opaque (no transparency)                  |
| Signature              | string           | Yes, for Available   | A hash of the pixel data, independent from image format/codec           |
| AttributeNames         | array of strings | Yes, for Available   | ImageMagick attribute names, e.g. EXIF or format-specific metadata      |

In the Required column of the table above, "existing" refers to Available or Error results. These are icons that can be downloaded.

## ResultType schema

| Enum value | Description                                                   |
| ---------- | ------------------------------------------------------------- |
| Available  | The icon is available and was opened successfully             |
| Deleted    | The package is deleted and no metadata is available           |
| Error      | The icon is available but could not be opened with Magick.NET |
| NoIcon     | The package exists but no icon is available                   |

## AttributeNames schema

The AttributeNames column is an array of strings. Each string is the name (key) of an attribute that ImageMagick discovered in the image file. They are often in the format of `{category}:{name}`.
