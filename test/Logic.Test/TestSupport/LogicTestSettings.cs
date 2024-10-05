// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Azure.Identity;

#nullable enable

namespace NuGet.Insights
{
    public static class LogicTestSettings
    {
        public static Lazy<Exception?> StorageConnectionError { get; } = new(GetStorageConnectionError);
        public static bool HasStorageConnectionError => StorageConnectionError.IsValueCreated && StorageConnectionError.Value is not null;

        static LogicTestSettings()
        {
            VerifyStorageConfiguration();
        }

        private const string UseDevelopmentStorageEnvName = "NUGETINSIGHTS_USEDEVELOPMENTSTORAGE";
        private const string UseMemoryStorageEnvName = "NUGETINSIGHTS_USEMEMORYSTORAGE";
        private const string StorageAccountNameEnvName = "NUGETINSIGHTS_STORAGEACCOUNTNAME";
        private const string StorageClientApplicationIdEnvName = "NUGETINSIGHTS_STORAGECLIENTAPPLICATIONID";
        private const string StorageClientTenantIdEnvName = "NUGETINSIGHTS_STORAGECLIENTTENANTID";
        private const string StorageClientCertificatePathEnvName = "NUGETINSIGHTS_STORAGECLIENTCERTIFICATEPATH";
        private const string StorageClientCertificateKeyVaultEnvName = "NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULT";
        private const string StorageClientCertificateKeyVaultCertificateNameEnvName = "NUGETINSIGHTS_STORAGECLIENTCERTIFICATEKEYVAULTCERTIFICATENAME";

        public static bool UseMemoryStorage => StorageCredentialType == StorageCredentialType.MemoryStorage;
        public static bool UseDevelopmentStorage => StorageCredentialType == StorageCredentialType.DevelopmentStorage;
        public static StorageCredentialType StorageCredentialType => PopulateSettings(new NuGetInsightsSettings()).GetStorageCredentialType();

        private static bool? UseDevelopmentStorageEnv => GetEnvBool(UseDevelopmentStorageEnvName);
        private static bool? UseMemoryStorageEnv => GetEnvBool(UseMemoryStorageEnvName);
        private static string? StorageAccountNameEnv => GetEnvOrNull(StorageAccountNameEnvName);
        private static string? StorageClientApplicationIdEnv => GetEnvOrNull(StorageClientApplicationIdEnvName);
        private static string? StorageClientTenantIdEnv => GetEnvOrNull(StorageClientTenantIdEnvName);
        private static string? StorageClientCertificatePathEnv => GetEnvOrNull(StorageClientCertificatePathEnvName);
        private static string? StorageClientCertificateKeyVaultEnv => GetEnvOrNull(StorageClientCertificateKeyVaultEnvName);
        private static string? StorageClientCertificateKeyVaultCertificateNameEnv => GetEnvOrNull(StorageClientCertificateKeyVaultCertificateNameEnvName);

        public static T WithTestStorageSettings<T>(this T settings) where T : NuGetInsightsSettings
        {
            PopulateSettings(settings);

            var ex = StorageConnectionError.Value;
            if (ex is not null)
            {
                throw ex;
            }

            return settings;
        }

        private static T PopulateSettings<T>(T settings) where T : NuGetInsightsSettings
        {
            settings.UseDevelopmentStorage = UseDevelopmentStorageEnv.GetValueOrDefault(false);
            settings.UseMemoryStorage = UseMemoryStorageEnv.GetValueOrDefault(false);
            settings.StorageAccountName = StorageAccountNameEnv;

            settings.StorageClientApplicationId = StorageClientApplicationIdEnv;
            settings.StorageClientTenantId = StorageClientTenantIdEnv;
            settings.StorageClientCertificatePath = StorageClientCertificatePathEnv;
            settings.StorageClientCertificateKeyVault = StorageClientCertificateKeyVaultEnv;
            settings.StorageClientCertificateKeyVaultCertificateName = StorageClientCertificateKeyVaultCertificateNameEnv;

            settings.UseAccessTokenCaching = true;

            // if no settings are provided, use in-memory storage
            if (UseMemoryStorageEnv.GetValueOrDefault(true)
                && !UseDevelopmentStorageEnv.GetValueOrDefault(false)
                && StorageAccountNameEnv is null)
            {
                settings.UseMemoryStorage = true;
            }

            return settings;
        }

        private static void VerifyStorageConfiguration()
        {
            if (UseMemoryStorageEnv == false
                && UseDevelopmentStorageEnv == false
                && StorageAccountNameEnv is null)
            {
                var names = string.Join(Environment.NewLine, Environment
                    .GetEnvironmentVariables()
                    .Keys
                    .OfType<string>()
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .Where(x => x.StartsWith("NUGETINSIGHTS_", StringComparison.OrdinalIgnoreCase))
                    .Select(x => $"  * {x}"));

                throw new InvalidOperationException(
                    $"""


                    ########################
                    # NUGET INSIGHTS TESTS #
                    ########################

                    No valid test storage configurations were found. Consider these options:

                    - Set {UseMemoryStorageEnvName} to 'true' (or unset it) to use in-memory storage.
                      * It evaluates to '{UseMemoryStorageEnv}' now.
                      * This is the easiest option.

                    - Set {UseDevelopmentStorageEnvName} to 'true' to use the storage emulator.
                      * It evaluates to '{UseDevelopmentStorageEnv}' now.
                      * You must start the storage emulator (e.g. Azurite) before running the tests.

                    - Set {StorageAccountNameEnvName} to the name of a real Azure storage account to use.
                      * Set an appropriate credential via environment variables, or use {nameof(DefaultAzureCredential)} as the default.
                      * Use {StorageClientApplicationIdEnvName} and {StorageClientTenantIdEnvName} to specify an app registration.
                      * Use {StorageClientCertificatePathEnvName} to specify a certificate file on disk.
                      * Use {StorageClientCertificateKeyVaultEnvName} and {StorageClientCertificateKeyVaultCertificateNameEnvName} to specify a certificate in Key Vault.
                      * Key Vault authentication uses {nameof(DefaultAzureCredential)}.

                    The following NUGETINSIGHTS_ environment variables have values:
                    {names}

                    Fix the environment variables and then try again.

                    """);
            }
        }

        private static Exception? GetStorageConnectionError()
        {
            NuGetInsightsSettings settings = new NuGetInsightsSettings();
            PopulateSettings(settings);

            var storageCredentialType = settings.GetStorageCredentialType();
            if (storageCredentialType == StorageCredentialType.MemoryStorage)
            {
                return null;
            }

            Uri blob;
            Uri queue;
            Uri table;
            if (storageCredentialType == StorageCredentialType.DevelopmentStorage)
            {
                blob = StorageUtility.DevelopmentBlobEndpoint;
                queue = StorageUtility.DevelopmentQueueEndpoint;
                table = StorageUtility.DevelopmentTableEndpoint;
            }
            else
            {
                blob = StorageUtility.GetBlobEndpoint(settings.StorageAccountName);
                queue = StorageUtility.GetQueueEndpoint(settings.StorageAccountName);
                table = StorageUtility.GetTableEndpoint(settings.StorageAccountName);
            }

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(20);

            foreach (var (endpoint, uri) in new[] { ("blob", blob), ("queue", queue), ("table", table) })
            {
                var attempt = 0;
                while (true)
                {
                    try
                    {
                        attempt++;

                        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                        using var response = httpClient.Send(request);

                        // any response is good enough for now
                        break;
                    }
                    catch when (attempt < 10)
                    {
                        // allow retry
                        Thread.Sleep(500 * attempt);
                    }
                    catch (Exception ex)
                    {
                        var messages = new StringBuilder();
                        var current = ex;
                        while (current is not null)
                        {
                            messages.AppendLine();
                            messages.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1}", current.GetType().FullName, current.Message);
                            current = current.InnerException;
                        }

                        messages.AppendLine();
                        messages.AppendLine();

                        if (storageCredentialType == StorageCredentialType.DevelopmentStorage)
                        {
                            messages.AppendLine("Ensure that the storage emulator (e.g. Azurite) is running.");
                        }
                        else
                        {
                            messages.AppendLine("Ensure that the storage account exists.");
                        }

                        return new InvalidOperationException(
                            $"""


                            ########################
                            # NUGET INSIGHTS TESTS #
                            ########################

                            The {endpoint} storage endpoint at {uri} is not responding.
                            The storage credential type is {storageCredentialType}.
                            The error is:
                            {messages}
                            """);
                    }
                }
            }

            return null;
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
