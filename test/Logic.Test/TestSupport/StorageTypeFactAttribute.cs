// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class StorageTypeFactAttribute : FactAttribute
    {
        public StorageTypeFactAttribute(StorageType storageType)
        {
            if (!storageType.HasFlag(LogicTestSettings.StorageType))
            {
                Skip = $"This Fact is skipped because the current storage type is '{LogicTestSettings.StorageType}', not '{storageType}'.";
            }
        }
    }
}
