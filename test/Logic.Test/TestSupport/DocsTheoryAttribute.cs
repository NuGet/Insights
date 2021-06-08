// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Insights
{
    public class DocsTheoryAttribute : TheoryAttribute
    {
        public DocsTheoryAttribute()
        {
            Skip = new DocsFactAttribute().Skip;
        }
    }
}
