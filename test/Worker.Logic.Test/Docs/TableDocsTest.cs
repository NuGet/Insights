// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig.Extensions.Tables;
using Markdig.Syntax;

#nullable enable

namespace NuGet.Insights.Worker
{
    public partial class TableDocsTest
    {
        [Theory]
        [MemberData(nameof(TableNameTestData))]
        public void TableIsDocumented(string tableName)
        {
            var info = new TableDocInfo(tableName);
            Assert.True(File.Exists(info.DocPath), $"The {tableName} table should be documented at {info.DocPath}");
        }

        [Theory]
        [MemberData(nameof(TableNameTestData))]
        public void TableDocHasNoTODO(string tableName)
        {
            var info = new TableDocInfo(tableName);
            info.ReadMarkdown();
            Assert.DoesNotContain("TODO", info.UnparsedMarkdown, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
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

        [Theory]
        [MemberData(nameof(TableNameTestData))]
        public void FirstTableIsGeneralTableProperties(string tableName)
        {
            var info = new TableDocInfo(tableName);
            var rows = info.GetFirstTableRows();

            var i = 0;
            Assert.Equal(2, rows[i].Count);
            Assert.Empty(info.ToPlainText(rows[i]));

            i++;
            Assert.Equal("Cardinality", info.ToPlainText(rows[i][0]));
            var cardinalityColumn = info.ToPlainText(rows[i][1]);
            Assert.NotEmpty(cardinalityColumn);
            var expectedCardinality = TableDocInitializer.GetCardinality(info.KeyFields);
            if (expectedCardinality is not null)
            {
                Assert.Equal(expectedCardinality, cardinalityColumn);
            }
            else
            {
                Assert.NotEqual(TableDocInitializer.GetCardinality([nameof(PackageRecord.LowerId)]), cardinalityColumn);
                Assert.NotEqual(TableDocInitializer.GetCardinality([nameof(PackageRecord.Identity)]), cardinalityColumn);
            }

            i++;
            Assert.Equal("Child tables", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Parent tables", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Column used for CSV partitioning", info.ToPlainText(rows[i][0]));
            var csvPartitioningColumn = info.ToPlainText(rows[i][1]);
            Assert.Contains(csvPartitioningColumn, info.NameToProperty);
            Assert.NotNull(info.CsvPartitioningKeyFieldName);
            Assert.Equal(info.CsvPartitioningKeyFieldName, csvPartitioningColumn);

            i++;
            Assert.Equal("Column used for Kusto partitioning", info.ToPlainText(rows[i][0]));
            var kustoPartitioningColumn = info.ToPlainText(rows[i][1]);
            Assert.Contains(kustoPartitioningColumn, info.NameToProperty);
            Assert.NotNull(info.KustoPartitioningKeyFieldName);
            Assert.Equal(info.KustoPartitioningKeyFieldName, kustoPartitioningColumn);

            i++;
            Assert.Equal("Key fields", info.ToPlainText(rows[i][0]));
            var keyFieldsString = info.ToPlainText(rows[i][1]);
            Assert.NotEmpty(keyFieldsString);
            Assert.NotNull(info.KeyFields);
            Assert.Equal(string.Join(", ", info.KeyFields), keyFieldsString);
            var keyFields = keyFieldsString.Split(", ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Assert.NotEmpty(keyFields);
            Assert.All(keyFields, x => Assert.Contains(x, info.NameToProperty));

            i++;
            Assert.Equal("Data file container name", info.ToPlainText(rows[i][0]));
            var containerName = info.ToPlainText(rows[i][1]);
            Assert.Equal(info.DefaultContainerName, containerName);

            i++;
            Assert.Equal("Driver", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Record type", info.ToPlainText(rows[i][0]));
            Assert.Equal(info.RecordType.Name, info.ToPlainText(rows[i][1]));

            i++;
            Assert.Equal(i, rows.Count);
        }

        [Theory]
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
                _output.WriteLine("Testing row: " + info.ToMarkdown(rowObj));
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

        [Theory]
        [MemberData(nameof(TableNameTestData))]
        public void AllDynamicColumnsAreDocumented(string tableName)
        {
            var info = new TableDocInfo(tableName);
            info.ReadMarkdown();

            var dynamicColumns = info
                .NameToProperty
                .Values
                .Where(x => x.GetCustomAttribute<KustoTypeAttribute>()?.KustoType == "dynamic")
                .Select(x => x.Name);
            var headings = info.GetHeadings();

            foreach (var name in dynamicColumns)
            {
                _output.WriteLine($"Testing column {name}");
                var heading = $"{name} schema";
                Assert.Contains(heading, headings);
            }
        }

        [Theory]
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

                var nextObj = info.GetNextObject(heading);
                if (nextObj is HtmlBlock htmlBlock && htmlBlock.Type == HtmlBlockType.Comment)
                {
                    var comment = info.ToPlainText(htmlBlock);
                    Assert.Equal("<!-- NO TABLE -->", comment);
                }
                else
                {
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
                        _output.WriteLine("Testing row: " + info.ToMarkdown(rowObj));
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

        public static IReadOnlyList<string> TableNames => NuGetInsightsWorkerLogicKustoDDL
            .TypeToDefaultTableName
            .Values
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        public static IEnumerable<object[]> TableNameTestData => TableNames.Select(x => new object[] { x });

        private readonly ITestOutputHelper _output;

        public TableDocsTest(ITestOutputHelper output)
        {
            _output = output;
        }
    }
}
