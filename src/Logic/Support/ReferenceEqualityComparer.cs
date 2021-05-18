// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NuGet.Insights
{
    public class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new ReferenceEqualityComparer<T>();

        public bool Equals([AllowNull] T x, [AllowNull] T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode([DisallowNull] T obj)
        {
            return obj.GetHashCode();
        }
    }
}
