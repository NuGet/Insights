// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;

namespace NuGet.Insights.Worker
{
    public class TestDataTest : BaseWorkerLogicIntegrationTest
    {
        public TestDataTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public void AllTestLeversAreOff()
        {
            Assert.Empty(typeof(TestLevers).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.Empty(typeof(TestLevers).GetFields(BindingFlags.Static | BindingFlags.NonPublic));

            var levers = typeof(TestLevers).GetFields(BindingFlags.Static | BindingFlags.Public);
            var sb = new StringBuilder();
            Assert.All(levers, lever =>
            {
                var value = lever.GetValue(obj: null);
                var defaultValue = lever.FieldType.IsValueType ? Activator.CreateInstance(lever.FieldType) : null;
                Assert.Equal(defaultValue, value);
            });
        }

        [Fact]
        public void HasNoDuplicateFiles()
        {
            var hasDuplicates = false;
            var foundAny = false;

            foreach (var directory in Directory.EnumerateDirectories(TestData))
            {
                // Find all files with the same size.
                var fileSizeToFiles = new Dictionary<long, List<string>>();
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    foundAny = true;

                    var fullPath = Path.GetFullPath(file);
                    var fileSize = new FileInfo(fullPath).Length;
                    if (!fileSizeToFiles.TryGetValue(fileSize, out var files))
                    {
                        files = new List<string>();
                        fileSizeToFiles.Add(fileSize, files);
                    }

                    files.Add(fullPath);
                }

                // Of the files with the same size, find duplicates by hash.
                var hashToFiles = new Dictionary<string, List<string>>();
                foreach (var files in fileSizeToFiles.Values.Where(x => x.Count > 1))
                {
                    foreach (var file in files)
                    {
                        using var sha256 = SHA256.Create();
                        var fileBytes = File.ReadAllBytes(file);
                        var hash = sha256.ComputeHash(fileBytes).ToBase64();

                        if (!hashToFiles.TryGetValue(hash, out var sameHash))
                        {
                            sameHash = new List<string>();
                            hashToFiles.Add(hash, sameHash);
                        }

                        sameHash.Add(file);
                    }
                }

                foreach (var pair in hashToFiles.Where(x => x.Value.Count > 1))
                {
                    Logger.LogInformation("Hash of duplicate files: {Hash}", pair.Key);
                    foreach (var file in pair.Value)
                    {
                        Logger.LogInformation(" - {File}", file);
                    }

                    hasDuplicates = true;
                }
            }

            Assert.False(hasDuplicates, "Some duplicate files were found.");
            Assert.True(foundAny);
        }

        [Fact]
        public async Task DeleteOldStorageContainers()
        {
            await CleanUpStorageContainers(IsOldStoragePrefix);
        }

        [KustoFact(Timeout = 5 * 60 * 1000)]
        public async Task DeleteOldKustoTables()
        {
            ConfigureWorkerSettings = x => x.WithTestKustoSettings();

            await CleanUpKustoTablesAsync(IsOldStoragePrefix);
        }

        /// <summary>
        /// This test generates a table of all storage names and their types. The test output is used as a reference
        /// for looking up container names found in logs. If this test fails, it means that the set of storage names
        /// has changesd and the test output needs to be updated. This can be done by setting <see cref="TestLevers.OverwriteTestData"/>
        /// to true and running the test again.
        /// </summary>
        [Fact]
        public void GeneratedStorageNames()
        {
            var storageNameProperties = GetStorageNameProperties(Options.Value);
            var header = new[] { "Storage Name", "Storage Type", "Property Name" };
            var dataRows = storageNameProperties
                .Select(x => new[] { x.Value.Replace(StoragePrefix, "{STORAGE PREFIX}", StringComparison.Ordinal), x.StorageType.ToString(), x.Name })
                .OrderBy(x => x[0], StringComparer.Ordinal)
                .ThenBy(x => x[1], StringComparer.Ordinal)
                .ThenBy(x => x[2], StringComparer.Ordinal);

            var sb = new StringBuilder();
            var columnWidths = header.Select((x, i) => Math.Max(x.Length, dataRows.Max(y => y[i].Length))).ToArray();
            sb.AppendLine(string.Join(" | ", header.Select((x, i) => x.PadRight(columnWidths[i]))));
            sb.AppendLine(string.Join(" | ", header.Select((x, i) => new string('-', columnWidths[i]))));
            foreach (var row in dataRows)
            {
                sb.AppendLine(string.Join(" | ", row.Select((x, i) => x.PadRight(columnWidths[i]))));
            }

            var table = sb.ToString().Replace(Environment.NewLine, "\n", StringComparison.OrdinalIgnoreCase);

            AssertEqualWithDiff(Path.Combine(TestData, nameof(GeneratedStorageNames), "table.md"), table);
        }

        private bool IsOldStoragePrefix(string name)
        {
            var match = LogicTestSettings.StoragePrefixPattern.Match(name);
            if (!match.Success)
            {
                return false;
            }

            var date = DateTimeOffset.ParseExact(match.Groups["Date"].Value, "yyMMdd", CultureInfo.InvariantCulture);
            if (DateTimeOffset.UtcNow - date < TimeSpan.FromDays(2))
            {
                return false;
            }

            return true;
        }
    }
}
