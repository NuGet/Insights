// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace NuGet.Insights
{
    public static class TestSettings
    {
        private const string StorageEmulatorConnectionString = StorageUtility.EmulatorConnectionString;

        public static bool IsStorageEmulator => StorageConnectionString == StorageEmulatorConnectionString;

        public static string StorageConnectionString
        {
            get
            {
                var accountName = Environment.GetEnvironmentVariable("NUGETINSIGHTS_STORAGEACCOUNTNAME");
                var sas = Environment.GetEnvironmentVariable("NUGETINSIGHTS_STORAGESAS");
                if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(sas))
                {
                    return StorageEmulatorConnectionString;
                }

                return $"AccountName={accountName};SharedAccessSignature={sas}";
            }
        }

        public static string StorageBlobReadSharedAccessSignature
        {
            get
            {
                var sas = Environment.GetEnvironmentVariable("NUGETINSIGHTS_STORAGEBLOBREADSAS");
                if (string.IsNullOrWhiteSpace(sas))
                {
                    return null;
                }

                return sas;
            }
        }

        public static readonly Regex StoragePrefixPattern = new Regex(@"t(?<Date>\d{6})[a-z234567]{10}");

        public static string NewStoragePrefix()
        {
            var randomBytes = new byte[6];
            ThreadLocalRandom.NextBytes(randomBytes);
            var storagePrefix = "t" + DateTimeOffset.UtcNow.ToString("yyMMdd") + randomBytes.ToTrimmedBase32();
            Assert.Matches(StoragePrefixPattern, storagePrefix);
            return storagePrefix;
        }

        public static string GetRepositoryRoot()
        {
            const string markerFile = "NuGet.config";
            var repoDir = Directory.GetCurrentDirectory();
            while (repoDir != null && !Directory.GetFiles(repoDir).Any(x => Path.GetFileName(x) == markerFile))
            {
                repoDir = Path.GetDirectoryName(repoDir);
            }

            Assert.NotNull(repoDir);

            return repoDir;
        }
    }
}
