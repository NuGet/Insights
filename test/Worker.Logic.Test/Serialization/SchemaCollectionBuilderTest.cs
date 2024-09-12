// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.ReferenceTracking;

namespace NuGet.Insights.Worker
{
    public class SchemaCollectionBuilderTest
    {
        [Fact]
        public void CanBuildTheSchemaCollection()
        {
            var collection = SchemaCollectionBuilder.Default.Build();
            Assert.NotNull(collection);
        }

        [Fact]
        public void AllCsvCompactMessageSchemaNamesStartWithCorrectPrefix()
        {
            var builder = SchemaCollectionBuilder.Default;
            var schemas = builder.GetDeserializers().Where(x => x.Type.IsGenericType && x.Type.GetGenericTypeDefinition() == typeof(CsvCompactMessage<>));
            Assert.All(schemas, x => Assert.StartsWith("cc.", x.Name, StringComparison.Ordinal));
            Assert.NotEmpty(schemas);
        }

        [Fact]
        public void AllCleanupOrphanRecordsMessageSchemaNamesStartWithCorrectPrefix()
        {
            var builder = SchemaCollectionBuilder.Default;
            var schemas = builder.GetDeserializers().Where(x => x.Type.IsGenericType && x.Type.GetGenericTypeDefinition() == typeof(CleanupOrphanRecordsMessage<>));
            Assert.All(schemas, x => Assert.StartsWith("co.", x.Name, StringComparison.Ordinal));
        }
    }
}
