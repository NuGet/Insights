// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageContentToCsv
{
    public enum PackageContentResultType
    {
        Deleted,
        NoContent,
        AllLoaded,
        PartiallyLoaded,
        InvalidZipEntry,
    }
}
