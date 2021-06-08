// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Syntax;
using Xunit;

namespace NuGet.Insights.Worker
{
    public class DocInfo
    {
        public DocInfo(string docPath)
        {
            DocPath = Path.Combine(TestSettings.GetRepositoryRoot(), "docs", docPath);
        }

        public string DocPath { get; }
        public string UnparsedMarkdown { get; private set; }
        public MarkdownPipeline Pipeline { get; private set; }
        public MarkdownDocument MarkdownDocument { get; private set; }

        public virtual void ReadMarkdown()
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
