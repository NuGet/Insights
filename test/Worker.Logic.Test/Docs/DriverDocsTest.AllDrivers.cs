// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig.Extensions.Tables;
using Markdig.Syntax;

#if DEBUG
using Markdig.Helpers;
using Markdig.Renderers.Roundtrip;
#endif

namespace NuGet.Insights.Worker
{
    public partial class DriverDocsTest
    {
        [Fact]
        public void AllDriversAreListedInREADME()
        {
            var info = new DocInfo(Path.Combine("drivers", "README.md"));
            info.ReadMarkdown();

            var table = info.GetTableAfterHeading("Drivers");

            // Verify the driver header
            var headerRow = Assert.IsType<TableRow>(table[0]);
            Assert.True(headerRow.IsHeader);
            Assert.Equal(2, headerRow.Count);
            Assert.Equal("Driver name", info.ToPlainText(headerRow[0]));
            Assert.Equal("Description", info.ToPlainText(headerRow[1]));

            // Verify we have the right number of rows
            var rows = table.Skip(1).ToList();

            // Verify table rows
            for (var i = 0; i < rows.Count && i < DriverNames.Count; i++)
            {
                Block rowObj = rows[i];
                Output.WriteLine("Testing row: " + info.ToMarkdown(rowObj));
                var row = Assert.IsType<TableRow>(rowObj);
                Assert.False(row.IsHeader);

                // Verify the column name exists and is in the proper order in the table
                var driverName = info.ToPlainText(row[0]);
                Assert.Contains(driverName, DriverNames);
                Assert.Equal(driverName, DriverNames[i]);

                // Verify the data type
                var description = info.ToPlainText(row[1]);
                Assert.NotEmpty(description);
            }

            Assert.Equal(DriverNames.Count, rows.Count);
        }

        [Fact]
        public void DriverDependencyGraphIsUpdated()
        {
            var info = new DocInfo(Path.Combine("drivers", "README.md"));
            info.ReadMarkdown();

            var codeBlock = info.GetFencedCodeBlockAfterHeading("Dependency graph");
            Assert.Equal("mermaid", codeBlock.Info);
            Assert.Equal('`', codeBlock.FencedChar);
            Assert.Equal(3, codeBlock.OpeningFencedCharCount);
            Assert.Equal(3, codeBlock.ClosingFencedCharCount);
            Assert.Equal(0, codeBlock.IndentCount);

            var expectedLines = new List<string>
            {
                "flowchart LR",
                $"    FlatContainer[<a href='https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource'>NuGet.org V3 package content</a>]"
            };
            foreach (var driverType in CatalogScanDriverMetadata.StartableDriverTypes.OrderBy(x => x.ToString(), StringComparer.Ordinal))
            {
                // Relative links are not supported.
                // See: https://github.com/orgs/community/discussions/46096
                // See: https://github.com/mermaid-js/mermaid/issues/2233
                // expectedLines.Add($"    {driverType}[<a href='./{driverType}.md'>{driverType}</a>]");

                if (CatalogScanDriverMetadata.DriverTypesWithNoDependencies.Contains(driverType))
                {
                    expectedLines.Add($"    FlatContainer --> {driverType}");
                }

                foreach (var dependency in CatalogScanDriverMetadata.GetDependencies(driverType).OrderBy(x => x.ToString(), StringComparer.Ordinal))
                {
                    expectedLines.Add($"    {dependency} --> {driverType}");
                }
            }

            var expected = string.Join(Environment.NewLine, expectedLines);
            var actual = string.Join(string.Empty, codeBlock.Lines);

#if DEBUG
            if (actual != expected)
            {
                var beforeCodeBlock = info.UnparsedMarkdown.Substring(0, codeBlock.Span.Start);
                var afterCodeBlock = info.UnparsedMarkdown.Substring(codeBlock.Span.End + 1);

                codeBlock.Lines.Clear();
                foreach (var line in expectedLines)
                {
                    codeBlock.Lines.Add(new StringSlice(line + Environment.NewLine));
                }

                using var writer = new StringWriter();
                var renderer = new RoundtripRenderer(writer);
                renderer.Write(codeBlock);
                File.WriteAllText(info.DocPath, beforeCodeBlock + writer.ToString().Trim() + afterCodeBlock);
            }
#else
            Assert.Equal(expected, actual);
#endif
        }
    }
}
