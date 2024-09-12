// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace NuGet.Insights.Worker
{
    public partial class TableDocsTest
    {
        [Fact]
        public void AllTablesAreListedInREADME()
        {
            var info = new DocInfo(Path.Combine("tables", "README.md"));
            info.ReadMarkdown();

            var table = info.GetTableAfterHeading("Tables");

            // Verify the table header
            var headerRow = Assert.IsType<TableRow>(table[0]);
            Assert.True(headerRow.IsHeader);
            Assert.Equal(2, headerRow.Count);
            Assert.Equal("Table name", info.ToPlainText(headerRow[0]));
            Assert.Equal("Description", info.ToPlainText(headerRow[1]));

            // Verify we have the right number of rows
            var rows = table.Skip(1).ToList();

            // Verify table rows
            for (var i = 0; i < rows.Count; i++)
            {
                Block rowObj = rows[i];
                _output.WriteLine("Testing row: " + info.ToMarkdown(rowObj));
                var row = Assert.IsType<TableRow>(rowObj);
                Assert.False(row.IsHeader);

                // Verify the column name exists and is in the proper order in the table
                var tableName = info.ToPlainText(row[0]);
                Assert.Contains(tableName, TableNames);
                Assert.Equal(tableName, TableNames[i]);

                // Verify the data type
                var description = info.ToPlainText(row[1]);
                Assert.NotEmpty(description);
            }

            Assert.Equal(rows.Count, TableNames.Count);
        }
    }
}
