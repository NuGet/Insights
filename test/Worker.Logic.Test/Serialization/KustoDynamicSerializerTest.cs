// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class KustoDynamicSerializerTest
    {
        [Fact]
        public void SerializesStringToUnorderedAsObject()
        {
            var kv = new Dictionary<string, object>
            {
                { "a", new HashSet<string> { "zz" } },
            };

            Assert.Throws<NotImplementedException>(() => KustoDynamicSerializer.Serialize(kv));
        }

        [Fact]
        public void SerializesStringToStringAsObject()
        {
            var kv = new Dictionary<string, object>
            {
                { "b", "yy" },
                { "a", "zz" },
                { "c", "xx" },
            };

            var json = KustoDynamicSerializer.Serialize(kv);

            Assert.Equal("{\"a\":\"zz\",\"b\":\"yy\",\"c\":\"xx\"}", json);
        }

        [Fact]
        public void SerializesStringToString()
        {
            var kv = new Dictionary<string, string>
            {
                { "b", "yy" },
                { "a", "zz" },
                { "c", "xx" },
            };

            var json = KustoDynamicSerializer.Serialize(kv);

            Assert.Equal("{\"a\":\"zz\",\"b\":\"yy\",\"c\":\"xx\"}", json);
        }
    }
}
