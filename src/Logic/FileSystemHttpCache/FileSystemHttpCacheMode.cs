// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.FileSystemHttpCache
{
    public enum FileSystemHttpCacheMode
    {
        Disabled = 1,
        ReadAndWriteOnlyMissing,
        WriteOnlyMissing,
        WriteAlways,
        ReadOnly,
    }
}
