// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.BuildVersionSet
{
    internal abstract class EntityReferenceCounter
    {
        public int Count;
        public abstract void Clear();
    }

    internal class EntityReferenceCounter<T> : EntityReferenceCounter
    {
        public T Value;

        public override void Clear()
        {
            Value = default;
        }
    }
}
