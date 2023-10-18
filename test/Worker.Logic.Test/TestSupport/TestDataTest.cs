// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class TestDataTest : BaseWorkerLogicIntegrationTest
    {
        public TestDataTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public void IsNotOverwritingTestData()
        {
            Assert.False(OverwriteTestData);
            Assert.False(DriverDocsTest.OverwriteDriverDocs);
        }

        [Fact]
        public void HasNoDuplicateFiles()
        {
            var hasDuplicates = false;

            foreach (var directory in Directory.EnumerateDirectories(TestData))
            {
                // Find all files with the same size.
                var fileSizeToFiles = new Dictionary<long, List<string>>();
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
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

            Assert.False(hasDuplicates);
        }

        [Fact]
        public async Task DeleteOldStorageContainers()
        {
            await CleanUpStorageContainers(IsOldStoragePrefix);
        }

        [KustoFact(Timeout = 5 * 60 * 1000)]
        public async Task DeleteOldKustoTables()
        {
            await CleanUpKustoTablesAsync(IsOldStoragePrefix);
        }

        private bool IsOldStoragePrefix(string name)
        {
            var match = TestSettings.StoragePrefixPattern.Match(name);
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
