// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    [Flags]
    public enum StorageType
    {
        Azure = 1 << 0,
        LegacyEmulator = 1 << 1,
        Azurite = 1 << 2,
    }
}
