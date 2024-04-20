// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace NuGet.Insights
{
    public static class LogicTestSettings
    {
        private const string StorageAccountNameEnv = "NUGETINSIGHTS_STORAGEACCOUNTNAME";
        private const string StorageClientApplicationIdEnv = "NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID";
        private const string StorageClientTenantIdEnv = "NUGETINSIGHTS_STORAGECLIENTTENANTID";
        private const string StorageClientCertificatePathEnv = "NUGETINSIGHTS_STORAGECLIENTCERTIFICATEPATH";
        private const string StorageClientCertificateKeyVaultEnv = "NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULT";
        private const string StorageClientCertificateKeyVaultCertificateNameEnv = "NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULTCERTIFICATENAME";
        private const string StorageSasEnv = "NUGETINSIGHTS_STORAGESAS";
        private const string StorageBlobReadSasEnv = "NUGETINSIGHTS_STORAGEBLOBREADSAS";

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

        private static string StorageAccountName => GetEnvOrNull(StorageAccountNameEnv);
        public static string StorageClientApplicationId => GetEnvOrNull(StorageClientApplicationIdEnv);
        private static string StorageClientTenantId => GetEnvOrNull(StorageClientTenantIdEnv);
        private static string StorageClientCertificatePath => GetEnvOrNull(StorageClientCertificatePathEnv);
        private static string StorageClientCertificateKeyVault => GetEnvOrNull(StorageClientCertificateKeyVaultEnv);
        private static string StorageClientCertificateKeyVaultCertificateName => GetEnvOrNull(StorageClientCertificateKeyVaultCertificateNameEnv);
        public static string StorageSharedAccessSignature => GetEnvOrNull(StorageSasEnv);
        private static string StorageBlobReadSharedAccessSignature => GetEnvOrNull(StorageBlobReadSasEnv);

        public static T WithTestStorageSettings<T>(this T settings) where T : NuGetInsightsSettings
        {
            settings.StorageAccountName = StorageAccountName;
            settings.StorageBlobReadSharedAccessSignature = StorageBlobReadSharedAccessSignature;
            settings.StorageClientApplicationId = StorageClientApplicationId;
            settings.StorageClientCertificateKeyVault = StorageClientCertificateKeyVault;
            settings.StorageClientCertificateKeyVaultCertificateName = StorageClientCertificateKeyVaultCertificateName;
            settings.StorageClientCertificatePath = StorageClientCertificatePath;
            settings.StorageClientTenantId = StorageClientTenantId;
            settings.StorageConnectionString = StorageConnectionString;
            return settings;
        }

        private static string StorageConnectionString
        {
            get
            {
                if (StorageAccountName is null)
                {
                    return StorageEmulatorConnectionString;
                }

                return $"AccountName={StorageAccountName};SharedAccessSignature={StorageSharedAccessSignature}";
            }
        }

        public static string GetEnvOrNull(string variable)
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
