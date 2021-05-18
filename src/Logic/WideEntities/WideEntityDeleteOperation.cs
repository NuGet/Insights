// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.WideEntities
{
    public class WideEntityDeleteOperation : WideEntityOperation
    {
        public WideEntityDeleteOperation(WideEntity existing)
            : base(existing.PartitionKey)
        {
            Existing = existing;
        }

        public WideEntity Existing { get; }
    }
}
