// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.WideEntities
{
    public class WideEntityInsertOrReplaceOperation : WideEntityOperation
    {
        public WideEntityInsertOrReplaceOperation(string partitionKey, string rowKey, ReadOnlyMemory<byte> content)
            : base(partitionKey)
        {
            RowKey = rowKey;
            Content = content;
        }

        public string RowKey { get; }
        public ReadOnlyMemory<byte> Content { get; }
    }
}
