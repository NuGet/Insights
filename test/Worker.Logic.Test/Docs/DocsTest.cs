// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class DocsTest
    {
        [DocsFact]
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
            Assert.Equal(rows.Count, TableNames.Count);

            // Verify table rows
            var index = 0;
            foreach (var rowObj in rows)
            {
                _output.WriteLine("Testing row: " + info.GetMarkdown(rowObj));
                var row = Assert.IsType<TableRow>(rowObj);
                Assert.False(row.IsHeader);

                // Verify the column name exists and is in the proper order in the table
                var tableName = info.ToPlainText(row[0]);
                Assert.Contains(tableName, TableNames);
                Assert.Equal(tableName, TableNames[index]);

                // Verify the data type
                var description = info.ToPlainText(row[1]);
                Assert.NotEmpty(description);

                index++;
            }
        }

        [DocsTheory]
        [MemberData(nameof(TableNameTestData))]
        public void TableIsDocumented(string tableName)
        {
            var info = new TableDocInfo(tableName);
            Assert.True(File.Exists(info.DocPath), $"The {tableName} table should be documented at {info.DocPath}");
        }

        [DocsTheory]
        [MemberData(nameof(TableNameTestData))]
        public void HasDefaultTableNameHeading(string tableName)
        {
            var info = new TableDocInfo(tableName);
            info.ReadMarkdown();

            var obj = info.MarkdownDocument.First();
            var heading = Assert.IsType<HeadingBlock>(obj);
            Assert.Equal(1, heading.Level);
            Assert.Equal(tableName, info.ToPlainText(heading));
        }

        [DocsTheory]
        [MemberData(nameof(TableNameTestData))]
        public void FirstTableIsGeneralTableProperties(string tableName)
        {
            var info = new TableDocInfo(tableName);
            info.ReadMarkdown();

            var table = info.MarkdownDocument.OfType<Table>().FirstOrDefault();
            Assert.NotNull(table);

            var rows = table.Cast<TableRow>().ToList();

            var i = 0;
            Assert.Equal(2, rows[i].Count);
            Assert.Empty(info.ToPlainText(rows[i]));

            i++;
            Assert.Equal("Cardinality", info.ToPlainText(rows[i][0]));
            Assert.NotEmpty(info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Child tables", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Parent tables", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Column used for partitioning", info.ToPlainText(rows[i][0]));
            var partitioningColumn = info.ToPlainText(rows[i][1]);
            Assert.Contains(partitioningColumn, info.NameToProperty);

            i++;
            Assert.Equal("Data file container name", info.ToPlainText(rows[i][0]));
            var containerName = info.ToPlainText(rows[i][1]);
            Assert.Equal(info.DefaultContainerName, containerName);

            i++;
            Assert.Equal("Driver implementation", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Record type", info.ToPlainText(rows[i][0]));
            Assert.Equal(info.RecordType.Name, info.ToPlainText(rows[i][1]));

            i++;
            Assert.Equal(i, rows.Count);
        }

        [DocsTheory]
        [MemberData(nameof(TableNameTestData))]
        public void TableSchemaMatchesRecordType(string tableName)
        {
            var info = new TableDocInfo(tableName);
            info.ReadMarkdown();
            var table = info.GetTableAfterHeading("Table schema");

            // Verify the table header
            var headerRow = Assert.IsType<TableRow>(table[0]);
            Assert.True(headerRow.IsHeader);
            Assert.Equal(4, headerRow.Count);
            Assert.Equal("Column name", info.ToPlainText(headerRow[0]));
            Assert.Equal("Data type", info.ToPlainText(headerRow[1]));
            Assert.Equal("Required", info.ToPlainText(headerRow[2]));
            Assert.Equal("Description", info.ToPlainText(headerRow[3]));

            // Verify we have the right number of rows
            var rows = table.Skip(1).ToList();
            Assert.Equal(rows.Count, info.NameToIndex.Count);

            // Get all of the properties on the record
            Assert.Equal(rows.Count, info.NameToProperty.Count);

            // Verify table rows
            var index = 0;
            foreach (var rowObj in rows)
            {
                _output.WriteLine("Testing row: " + info.GetMarkdown(rowObj));
                var row = Assert.IsType<TableRow>(rowObj);
                Assert.False(row.IsHeader);

                // Verify the column name exists and is in the proper order in the table
                var columnName = info.ToPlainText(row[0]);
                Assert.Contains(columnName, info.NameToIndex.Keys);
                Assert.Contains(columnName, info.NameToProperty.Keys);
                Assert.Equal(info.NameToIndex[columnName], index);

                // Verify the data type
                var dataType = info.ToPlainText(row[1]);
                var property = info.NameToProperty[columnName];
                AssertDataTypeMatchesProperty(dataType, property);

                index++;
            }
        }

        [DocsTheory]
        [MemberData(nameof(TableNameTestData))]
        public void AllDynamicColumnsAreDocumented(string tableName)
        {
            var info = new TableDocInfo(tableName);
            info.ReadMarkdown();

            var dynamicColumns = info
                .NameToProperty
                .Values
                .Where(x => x.GetAttribute<KustoTypeAttribute>()?.KustoType == "dynamic")
                .Select(x => x.Name);
            var headings = info.GetHeadings();

            foreach (var name in dynamicColumns)
            {
                _output.WriteLine($"Testing column {name}");
                var heading = $"{name} schema";
                Assert.Contains(heading, headings);
            }
        }

        [DocsTheory]
        [MemberData(nameof(TableNameTestData))]
        public void AllEnumsAreDocumented(string tableName)
        {
            var info = new TableDocInfo(tableName);
            info.ReadMarkdown();

            var enumTypes = info
                .NameToProperty
                .Values
                .Select(x => TableDocInfo.TryGetEnumType(x.PropertyType, out var enumType) ? (x.Name, enumType) : default)
                .Where(x => x != default);
            var headings = info.GetHeadings();

            foreach ((var propertyName, var enumType) in enumTypes)
            {
                _output.WriteLine($"Testing enum {propertyName} [{enumType.FullName}]");
                var heading = $"{propertyName} schema";
                Assert.Contains(heading, headings);

                var table = info.GetTableAfterHeading(heading);

                // Verify the table header
                var headerRow = Assert.IsType<TableRow>(table[0]);
                Assert.True(headerRow.IsHeader);
                Assert.InRange(headerRow.Count, 1, 2);
                Assert.Equal("Enum value", info.ToPlainText(headerRow[0]));

                var rows = table.Skip(1).ToList();
                var enumNames = Enum
                    .GetNames(enumType)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                Assert.Equal(enumNames.Count, rows.Count);

                // Verify table rows
                var index = 0;
                foreach (var rowObj in rows)
                {
                    _output.WriteLine("Testing row: " + info.GetMarkdown(rowObj));
                    var row = Assert.IsType<TableRow>(rowObj);
                    Assert.False(row.IsHeader);

                    // Verify the enum name exists and is in the proper order in the table
                    var enumName = info.ToPlainText(row[0]);
                    Assert.Contains(enumName, enumNames);
                    Assert.Equal(enumNames[index], enumName);

                    index++;
                }
            }
        }

        private static void AssertDataTypeMatchesProperty(string dataType, PropertyInfo property)
        {
            var propertyType = property.PropertyType;
            if (TableDocInfo.IsDynamic(property))
            {
                Assert.Equal(typeof(string), propertyType);
                Assert.Contains(dataType, new[] { "object", "array of objects", "array of strings" });
            }
            else
            {
                var expectedDataType = TableDocInfo.GetExpectedDataType(property);
                if (expectedDataType is null)
                {
                    throw new InvalidDataException($"Unknown data type '{dataType}' found for a property with type {propertyType.FullName}.");
                }

                Assert.Equal(expectedDataType, dataType);
            }
        }

        public static IReadOnlyList<string> TableNames => KustoDDL
            .TypeToDefaultTableName
            .Values
            .OrderBy(x => x)
            .ToList();

        public static IEnumerable<object[]> TableNameTestData => TableNames.Select(x => new object[] { x });

        private readonly ITestOutputHelper _output;

        public DocsTest(ITestOutputHelper output)
        {
            _output = output;
        }
    }
}
