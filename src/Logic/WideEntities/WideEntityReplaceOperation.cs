// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.WideEntities
{
    public class WideEntityReplaceOperation : WideEntityOperation
    {
        public WideEntityReplaceOperation(WideEntity existing, ReadOnlyMemory<byte> content)
            : base(existing.PartitionKey)
        {
            Existing = existing;
            Content = content;
        }

        public WideEntity Existing { get; }
        public ReadOnlyMemory<byte> Content { get; }
    }
}
