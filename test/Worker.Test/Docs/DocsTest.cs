// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace NuGet.Insights.Worker
{
    public class DocsTest
    {
        [Theory]
        [MemberData(nameof(TableNames))]
        public void TableIsDocumented(string tableName)
        {
            var path = GetDocPath(tableName);
            Assert.True(File.Exists(path), $"The {tableName} table should be documented at {path}");
        }

        private static string GetDocPath(string tableName)
        {
            return Path.Combine(TestSettings.GetRepositoryRoot(), "docs", "tables", $"{tableName}.md");
        }

        public static IEnumerable<object[]> TableNames => KustoDDL
            .TypeToDefaultTableName
            .Values
            .OrderBy(x => x)
            .Select(x => new object[] { x });
    }
}
