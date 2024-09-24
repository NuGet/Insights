// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using System.Text.RegularExpressions;

#nullable enable

namespace NuGet.Insights
{
    public static class LogicTestSettings
    {
        private const string UseDevelopmentStorageEnvName = "NUGETINSIGHTS_USEDEVELOPMENTSTORAGE";
        private const string StorageAccountNameEnvName = "NUGETINSIGHTS_STORAGEACCOUNTNAME";
        private const string StorageClientApplicationIdEnvName = "NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID";
        private const string StorageClientTenantIdEnvName = "NUGETINSIGHTS_STORAGECLIENTTENANTID";
        private const string StorageClientCertificatePathEnvName = "NUGETINSIGHTS_STORAGECLIENTCERTIFICATEPATH";
        private const string StorageClientCertificateKeyVaultEnvName = "NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULT";
        private const string StorageClientCertificateKeyVaultCertificateNameEnvName = "NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULTCERTIFICATENAME";

        public static bool UseDevelopmentStorage => StorageCredentialType == StorageCredentialType.DevelopmentStorage;
        public static StorageCredentialType StorageCredentialType => ServiceClientFactory.GetStorageCredentialType(new NuGetInsightsSettings().WithTestStorageSettings());

        private static bool? UseDevelopmentStorageEnv => GetEnvBool(UseDevelopmentStorageEnvName);
        private static string? StorageAccountNameEnv => GetEnvOrNull(StorageAccountNameEnvName);
        private static string? StorageClientApplicationIdEnv => GetEnvOrNull(StorageClientApplicationIdEnvName);
        private static string? StorageClientTenantIdEnv => GetEnvOrNull(StorageClientTenantIdEnvName);
        private static string? StorageClientCertificatePathEnv => GetEnvOrNull(StorageClientCertificatePathEnvName);
        private static string? StorageClientCertificateKeyVaultEnv => GetEnvOrNull(StorageClientCertificateKeyVaultEnvName);
        private static string? StorageClientCertificateKeyVaultCertificateNameEnv => GetEnvOrNull(StorageClientCertificateKeyVaultCertificateNameEnvName);

        public static T WithTestStorageSettings<T>(this T settings) where T : NuGetInsightsSettings
        {
            settings.UseDevelopmentStorage = UseDevelopmentStorageEnv.GetValueOrDefault(false);
            settings.StorageAccountName = StorageAccountNameEnv;

            settings.StorageClientApplicationId = StorageClientApplicationIdEnv;
            settings.StorageClientTenantId = StorageClientTenantIdEnv;
            settings.StorageClientCertificatePath = StorageClientCertificatePathEnv;
            settings.StorageClientCertificateKeyVault = StorageClientCertificateKeyVaultEnv;
            settings.StorageClientCertificateKeyVaultCertificateName = StorageClientCertificateKeyVaultCertificateNameEnv;

            // if no settings are provided, use storage emulator
            if (!UseDevelopmentStorageEnv.HasValue
                && StorageAccountNameEnv is null)
            {
                settings.UseDevelopmentStorage = true;
                settings.StorageAccountName = null;
            }

            return settings;
        }

        public static string? GetEnvOrNull(string variable)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        public static bool? GetEnvBool(string variable)
        {
            var value = GetEnvOrNull(variable);
            if (value is null
                || !bool.TryParse(value, out var output))
            {
                return null;
            }

            return output;
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
