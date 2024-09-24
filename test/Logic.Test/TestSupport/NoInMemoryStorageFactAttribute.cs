// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class NoInMemoryStorageFactAttribute : FactAttribute
    {
        public NoInMemoryStorageFactAttribute()
        {
            if (LogicTestSettings.UseMemoryStorage)
            {
                Skip = "This Fact is skipped because it is not compatible with in-memory storage.";
            }
        }
    }
}
