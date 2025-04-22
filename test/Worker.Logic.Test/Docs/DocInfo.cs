// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Syntax;

#nullable enable

namespace NuGet.Insights.Worker
{
    public class DocInfo
    {
        public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .EnableTrackTrivia()
            .Build();

        private string? _unparsedMarkdown;
        private MarkdownDocument? _markdownDocument;

        public DocInfo(string docPath)
        {
            DocPath = Path.Combine(DirectoryHelper.GetRepositoryRoot(), "docs", docPath);
        }

        public string DocPath { get; }

        public string UnparsedMarkdown
        {
            get
            {
                if (_unparsedMarkdown is null)
                {
                    ReadMarkdown();
                }

                return _unparsedMarkdown!;
            }
        }

        public MarkdownDocument MarkdownDocument
        {
            get
            {
                if (_markdownDocument is null)
                {
                    ReadMarkdown();
                }

                return _markdownDocument!;
            }
        }

        public virtual void ReadMarkdown()
        {
            _unparsedMarkdown = BaseLogicIntegrationTest.ReadAllTextWithRetry(DocPath);
            _markdownDocument = ReadMarkdown(_unparsedMarkdown);
        }

        public static MarkdownDocument ReadMarkdown(string unparsedMarkdown)
        {
            return Markdown.Parse(unparsedMarkdown, Pipeline);
        }

        public string ToMarkdown(MarkdownObject obj)
        {
            return UnparsedMarkdown.Substring(obj.Span.Start, obj.Span.Length).Trim();
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
            return Assert.IsType<Table>(GetNextObject(heading));
        }

        public FencedCodeBlock GetFencedCodeBlockAfterHeading(string heading)
        {
            return Assert.IsType<FencedCodeBlock>(GetNextObject(heading));
        }

        public Block GetNextObject(string heading)
        {
            var nextObj = MarkdownDocument
                .SkipWhile(x => !(x is HeadingBlock) || ToPlainText(x) != heading)
                .Skip(1)
                .Where(x => x is not ParagraphBlock)
                .FirstOrDefault();
            Assert.NotNull(nextObj);
            return nextObj;
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
