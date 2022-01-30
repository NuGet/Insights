// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace NuGet.Insights
{
    public class JsonElementComparer : IEqualityComparer<JsonElement>
    {
        public static JsonElementComparer Instance { get; } = new JsonElementComparer();

        public bool Equals(JsonElement x, JsonElement y)
        {
            return x.ToString() == y.ToString();
        }

        public int GetHashCode([DisallowNull] JsonElement obj)
        {
            return obj.ToString().GetHashCode();
        }
    }
}
