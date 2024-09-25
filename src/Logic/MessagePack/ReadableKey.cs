// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static class ReadableKey
    {
        public static ReadableKey<T> Create<T>(string key, T Value)
        {
            return new ReadableKey<T>(key, Value);
        }
    }

    public class ReadableKey<T>
    {
        public ReadableKey(string key, T value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; set; }
        public T Value { get; set; }
    }
}
