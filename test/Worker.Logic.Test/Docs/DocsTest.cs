// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Castle.Core.Internal;
using Humanizer;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class DocsTest
    {
        [Theory]
        [MemberData(nameof(TableNames))]
        public void TableIsDocumented(string tableName)
        {
            var info = new TableInfo(tableName);
            Assert.True(File.Exists(info.DocPath), $"The {tableName} table should be documented at {info.DocPath}");
        }

        [Theory]
        [MemberData(nameof(TableNames))]
        public void HasDefaultTableNameHeading(string tableName)
        {
            var info = new TableInfo(tableName);
            info.ReadMarkdown();

            var obj = info.MarkdownDocument.First();
            var heading = Assert.IsType<HeadingBlock>(obj);
            Assert.Equal(1, heading.Level);
            Assert.Equal(tableName, info.ToPlainText(heading));
        }

        [Theory]
        [MemberData(nameof(TableNames))]
        public void FirstTableIsGeneralTableProperties(string tableName)
        {
            var info = new TableInfo(tableName);
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
            var settings = new NuGetInsightsWorkerSettings();
            var settingsProperties = settings.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var containerNamePropertyName = $"{tableName.Singularize()}ContainerName";
            Assert.Contains(containerNamePropertyName, settingsProperties.Select(x => x.Name).Where(x => x.EndsWith("ContainerName")));
            var defaultContainerName = Assert.IsType<string>(settingsProperties.Single(x => x.Name == containerNamePropertyName).GetValue(settings));
            Assert.Equal(defaultContainerName, containerName);

            i++;
            Assert.Equal("Driver implementation", info.ToPlainText(rows[i][0]));

            i++;
            Assert.Equal("Record type", info.ToPlainText(rows[i][0]));
            Assert.Equal(info.RecordType.Name, info.ToPlainText(rows[i][1]));

            i++;
            Assert.Equal(i, rows.Count);
        }

        [Theory]
        [MemberData(nameof(TableNames))]
        public void TableSchemaMatchesRecordType(string tableName)
        {
            var info = new TableInfo(tableName);
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

        [Theory]
        [MemberData(nameof(TableNames))]
        public void AllDynamicColumnsAreDocumented(string tableName)
        {
            var info = new TableInfo(tableName);
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

        [Theory]
        [MemberData(nameof(TableNames))]
        public void AllEnumsAreDocumented(string tableName)
        {
            var info = new TableInfo(tableName);
            info.ReadMarkdown();

            var enumTypes = info
                .NameToProperty
                .Values
                .Select(x => TryGetEnumType(x.PropertyType, out var enumType) ? (x.Name, enumType) : default)
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
            if (property.GetAttribute<KustoTypeAttribute>()?.KustoType == "dynamic")
            {
                Assert.Equal(typeof(string), propertyType);
                Assert.Contains(dataType, new[] { "object", "array of objects", "array of strings" });
            }
            else if (propertyType == typeof(string) || propertyType == typeof(Guid?) || propertyType == typeof(Version))
            {
                Assert.Equal("string", dataType);
            }
            else if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            {
                Assert.Equal("bool", dataType);
            }
            else if (propertyType == typeof(ushort) || propertyType == typeof(ushort?))
            {
                Assert.Equal("ushort", dataType);
            }
            else if (propertyType == typeof(uint) || propertyType == typeof(uint?))
            {
                Assert.Equal("uint", dataType);
            }
            else if (propertyType == typeof(int) || propertyType == typeof(int?))
            {
                Assert.Equal("int", dataType);
            }
            else if (propertyType == typeof(long) || propertyType == typeof(long?))
            {
                Assert.Equal("long", dataType);
            }
            else if (propertyType == typeof(DateTimeOffset) || propertyType == typeof(DateTimeOffset?))
            {
                Assert.Equal("timestamp", dataType);
            }
            else if (propertyType.IsEnum && propertyType.GetAttribute<FlagsAttribute>() != null)
            {
                Assert.Equal("comma separated enum", dataType);
            }
            else if (TryGetEnumType(propertyType, out var enumType))
            {
                if (enumType.GetAttribute<FlagsAttribute>() != null)
                {
                    Assert.Equal("comma separated enum", dataType);
                }
                else
                {
                    Assert.Equal("enum", dataType);
                }
            }
            else
            {
                throw new InvalidDataException($"Unknown data type '{dataType}' found for a property with type {propertyType.FullName}.");
            }
        }

        public static bool TryGetEnumType(Type type, out Type enumType)
        {
            enumType = type;

            if (type.IsEnum)
            {
                return true;
            }

            if (type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(Nullable<>)
                && type.GenericTypeArguments[0].IsEnum)
            {
                enumType = type.GenericTypeArguments[0];
                return true;
            }

            return false;
        }

        public static IEnumerable<object[]> TableNames => KustoDDL
            .TypeToDefaultTableName
            .Values
            .OrderBy(x => x)
            .Select(x => new object[] { x });

        private readonly ITestOutputHelper _output;

        public DocsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private class TableInfo
        {
            public TableInfo(string tableName)
            {
                DocPath = Path.Combine(TestSettings.GetRepositoryRoot(), "docs", "tables", $"{tableName}.md");

                RecordType = KustoDDL.TypeToDefaultTableName.Single(x => x.Value == tableName).Key;

                var recordInstance = (ICsvRecord)Activator.CreateInstance(RecordType);
                using var csvHeaderWriter = new StringWriter();
                recordInstance.WriteHeader(csvHeaderWriter);
                NameToIndex = csvHeaderWriter
                    .ToString()
                    .Split(',')
                    .Select((x, i) => (x, i))
                    .ToDictionary(x => x.x.Trim(), x => x.i);

                var nameToProperty = RecordType
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .ToDictionary(x => x.Name);
                nameToProperty.Remove(nameof(ICsvRecord.FieldCount));
                NameToProperty = nameToProperty;
            }

            public string TableName { get; }
            public string DocPath { get; }
            public Type RecordType { get; }
            public IReadOnlyDictionary<string, int> NameToIndex { get; }
            public IReadOnlyDictionary<string, PropertyInfo> NameToProperty { get; }

            public string UnparsedMarkdown { get; private set; }
            public MarkdownPipeline Pipeline { get; private set; }
            public MarkdownDocument MarkdownDocument { get; private set; }

            public void ReadMarkdown()
            {
                UnparsedMarkdown = File.ReadAllText(DocPath);
                Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
                MarkdownDocument = Markdown.Parse(UnparsedMarkdown, Pipeline);
            }

            public string GetMarkdown(MarkdownObject obj)
            {
                return UnparsedMarkdown.Substring(obj.Span.Start, obj.Span.Length);
            }

            public IReadOnlyList<string> GetHeadings()
            {
                return MarkdownDocument
                    .OfType<HeadingBlock>()
                    .Select(x => ToPlainText(x))
                    .ToList();
            }

            public Table GetTableAfterHeading(string heading)
            {
                var nextObj = MarkdownDocument
                    .SkipWhile(x => !(x is HeadingBlock) || ToPlainText(x) != heading)
                    .Skip(1)
                    .Where(x => x is not ParagraphBlock)
                    .FirstOrDefault();
                Assert.NotNull(nextObj);
                return Assert.IsType<Table>(nextObj);
            }

            public string ToPlainText(MarkdownObject obj, bool trim = true)
            {
                using var writer = new StringWriter();
                var render = new HtmlRenderer(writer)
                {
                    EnableHtmlForBlock = false,
                    EnableHtmlForInline = false,
                    EnableHtmlEscape = false,
                };
                Pipeline.Setup(render);

                render.Render(obj);
                writer.Flush();

                var output = writer.ToString();
                return trim ? output.Trim() : output;
            }
        }
    }
}
