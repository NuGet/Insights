// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights
{
    [Flags]
    public enum OSPlatformType
    {
        Windows = 1 << 0,
        Linux = 1 << 1,
        OSX = 1 << 2,
        FreeBSD = 1 << 3,
    }
}
