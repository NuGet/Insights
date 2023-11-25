// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class DocsFactAttribute : FactAttribute
    {
        public DocsFactAttribute()
        {
#if !ENABLE_NPE || !ENABLE_CRYPTOAPI
            Skip = "This Fact is skipped because ENABLE_NPE or ENABLE_CRYPTOAPI is not defined.";
#endif
        }
    }
}
