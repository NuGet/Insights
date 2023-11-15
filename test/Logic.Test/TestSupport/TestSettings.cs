// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

namespace NuGet.Insights
{
    public static class TestSettings
    {
        public const string StorageAccountNameEnv = "NUGETINSIGHTS_STORAGEACCOUNTNAME";
        public const string StorageSasEnv = "NUGETINSIGHTS_STORAGESAS";
        public const string StorageBlobReadSasEnv = "NUGETINSIGHTS_STORAGEBLOBREADSAS";
        public const string KustoConnectionStringEnv = "NUGETINSIGHTS_KUSTOCONNECTIONSTRING";
        public const string KustoDatabaseNameEnv = "NUGETINSIGHTS_KUSTODATABASENAME";
        public const string KustoClientCertificateEnv = "NUGETINSIGHTS_KUSTOCLIENTCERTIFICATE";

        private static readonly Lazy<StorageType> LazyStorageType = new Lazy<StorageType>(() =>
        {
            if (!IsStorageEmulator)
            {
                return StorageType.Azure;
            }
            else
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // The legacy emulator does not work on non-Windows platforms.
                    return StorageType.Azurite;
                }

                try
                {
                    using var httpClient = new HttpClient();
                    using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:10000/devstoreaccount1");
                    using var response = httpClient.Send(request);

                    if (response.Headers.TryGetValues("Server", out var serverHeaders)
                        && serverHeaders.Any(x => x.Contains("Azurite", StringComparison.Ordinal)))
                    {
                        // Azurite returns a header like this: "Server: Azurite-Blob/3.16.0"
                        return StorageType.Azurite;
                    }
                }
                catch
                {
                    // Ignore this exception
                }

                return StorageType.LegacyEmulator;
            }
        });

        private const string StorageEmulatorConnectionString = StorageUtility.EmulatorConnectionString;

        public static bool IsStorageEmulator => StorageConnectionString == StorageEmulatorConnectionString;
        public static StorageType StorageType => LazyStorageType.Value;

        public static string KustoConnectionString => GetEnvOrNull(KustoConnectionStringEnv);
        public static string KustoDatabaseName => GetEnvOrNull(KustoDatabaseNameEnv);
        public static string KustoClientCertificateContent => GetEnvOrNull(KustoClientCertificateEnv);

        public static string StorageAccountName => GetEnvOrNull(StorageAccountNameEnv);
        public static string StorageSharedAccessSignature => GetEnvOrNull(StorageSasEnv);
        public static string StorageBlobReadSharedAccessSignature => GetEnvOrNull(StorageBlobReadSasEnv);

        public static string StorageConnectionString
        {
            get
            {
                if (StorageAccountName is null || StorageSharedAccessSignature is null)
                {
                    return StorageEmulatorConnectionString;
                }

                return $"AccountName={StorageAccountName};SharedAccessSignature={StorageSharedAccessSignature}";
            }
        }

        private static string GetEnvOrNull(string variable)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value;
        }

        private const string StoragePrefixPatternString = @"t(?<Date>\d{6})[a-z234567]{16}";
        public static readonly Regex StoragePrefixPattern = new Regex(StoragePrefixPatternString);

        public static string NewStoragePrefix()
        {
            var randomBytes = new byte[10];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var storagePrefix = "t" + DateTimeOffset.UtcNow.ToString("yyMMdd", CultureInfo.InvariantCulture) + randomBytes.ToTrimmedBase32();
            Assert.Matches("^" + StoragePrefixPattern + "$", storagePrefix);
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
