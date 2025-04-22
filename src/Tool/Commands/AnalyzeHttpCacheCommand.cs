// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;
using DiffPlex;
using DiffPlex.Chunkers;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Insights.Worker;

namespace NuGet.Insights.Tool
{
    public class AnalyzeHttpCacheCommand : ICommand
    {
        private readonly ILogger<AnalyzeHttpCacheCommand> _logger;

        public AnalyzeHttpCacheCommand(ILogger<AnalyzeHttpCacheCommand> logger)
        {
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var testSettings = new NuGetInsightsWorkerSettings().WithTestStorageSettings();
            var cacheDirectory = Path.GetFullPath(testSettings.HttpCacheDirectory);
            _logger.LogInformation("File system cache directory: {Directory}", cacheDirectory);

            var directoryToRequestTypeToInfoFiles = ExploreCacheDirectory(cacheDirectory);

            foreach (var (directory, requestTypeToInfoFiles) in directoryToRequestTypeToInfoFiles)
            {
                foreach (var (requestType, infoFiles) in requestTypeToInfoFiles)
                {
                    if (infoFiles.Count == 1)
                    {
                        continue;
                    }

                    if (requestType == "G_R")
                    {
                        continue;
                    }

                    for (var i = 1; i < infoFiles.Count; i++)
                    {
                        Console.WriteLine(new string('-', 40));
                        Console.WriteLine("Comparing:");
                        Console.WriteLine("  " + Path.GetRelativePath(cacheDirectory, infoFiles[i - 1].FullName));
                        Console.WriteLine("  " + Path.GetRelativePath(cacheDirectory, infoFiles[i].FullName));
                        Console.WriteLine();

                        var a = File.ReadAllText(infoFiles[i - 1].FullName);
                        var b = File.ReadAllText(infoFiles[i].FullName);
                        var diff = InlineDiffBuilder.Diff(a, b, ignoreWhiteSpace: false, ignoreCase: false, chunker: new LineEndingsPreservingChunker());

                        var savedColor = Console.ForegroundColor;
                        foreach (var line in diff.Lines)
                        {
                            switch (line.Type)
                            {
                                case ChangeType.Inserted:
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.Write("+ ");
                                    break;
                                case ChangeType.Deleted:
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.Write("- ");
                                    break;
                                default:
                                    Console.ForegroundColor = ConsoleColor.Gray; // compromise for dark or light background
                                    Console.Write("  ");
                                    break;
                            }

                            var viewLine = line.Text
                                .Replace("\r", "\\r", StringComparison.Ordinal)
                                .Replace("\n", "\\n", StringComparison.Ordinal)
                                .Replace("\t", "\\t", StringComparison.Ordinal);
                            Console.WriteLine(viewLine);
                        }
                        Console.ForegroundColor = savedColor;

                        Console.WriteLine();
                    }
                }
            }

            await Task.Yield();
        }

        private static Dictionary<string, Dictionary<string, List<FileInfo>>> ExploreCacheDirectory(string cacheDirectory)
        {
            var directoryToRequestTypeToInfoFiles = new Dictionary<string, Dictionary<string, List<FileInfo>>>();
            foreach (var file in Directory.EnumerateFiles(cacheDirectory, "*_i.json", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(file);
                var match = Regex.Match(fileName, "(?<RequestType>G|H|G_R)_(?<Hash>[a-z0-9]{7,})_(?<FileType>[a-z]+)(?<Extension>\\..+)");
                var requestType = match.Groups["RequestType"].Value;
                if (!match.Success)
                {
                    throw new InvalidOperationException($"Unexpected file name format: {file}");
                }

                var relativeFile = Path.GetRelativePath(cacheDirectory, file);
                var relativeDir = Path.GetDirectoryName(relativeFile);
                if (!directoryToRequestTypeToInfoFiles.TryGetValue(relativeDir, out var requestTypeToInfoFiles))
                {
                    requestTypeToInfoFiles = [];
                    directoryToRequestTypeToInfoFiles.Add(relativeDir, requestTypeToInfoFiles);
                }

                if (!requestTypeToInfoFiles.TryGetValue(requestType, out var infoFiles))
                {
                    infoFiles = new List<FileInfo>();
                    requestTypeToInfoFiles.Add(requestType, infoFiles);
                }

                infoFiles.Add(new FileInfo(file));
            }

            return directoryToRequestTypeToInfoFiles;
        }
    }

    public class LineEndingsAsChunk : IChunker
    {
        public string[] Chunk(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return [];
            }

            List<string> list = new List<string>();
            int num = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n' || c == '\r')
                {
                    var ci = i;

                    i++;
                    if (c == '\r' && i < text.Length && text[i] == '\n')
                    {
                        i++;
                    }

                    string item1 = text.Substring(num, ci - num);
                    num = ci;
                    list.Add(item1);
                    string item2 = text.Substring(num, i - num);
                    num = i;
                    list.Add(item2);
                }
            }

            if (num != text.Length)
            {
                string item2 = text.Substring(num, text.Length - num);
                list.Add(item2);
            }

            return list.ToArray();
        }
    }
}
