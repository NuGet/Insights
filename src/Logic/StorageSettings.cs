// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class StorageSettings
    {
        public bool UseMemoryStorage { get; set; } = false;
        public bool UseDevelopmentStorage { get; set; } = false;

        public string StorageAccountName { get; set; } = null;
        public string StorageClientApplicationId { get; set; } = null;
        public string StorageClientTenantId { get; set; } = null;
        public string StorageClientCertificatePath { get; set; } = null;
        public string StorageClientCertificateKeyVault { get; set; } = null;
        public string StorageClientCertificateKeyVaultCertificateName { get; set; } = null;
        public string StorageAccessKey { get; set; } = null;

        /// <summary>
        /// This is a relatively short period of time to allow the internal HTTP client to be released and refreshed
        /// frequently.
        /// </summary>
        public TimeSpan ServiceClientRefreshPeriod { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan ServiceClientSasDuration { get; set; } = TimeSpan.FromHours(12);

        /// <summary>
        /// This defaults to false because credential caching is typically done inside the service clients, which are
        /// long lived via <see cref="ServiceClientFactory"/> and similar. Additional caching is not needed.
        /// </summary>
        public bool UseAccessTokenCaching { get; set; } = false;

        public int AzureServiceClientMaxRetries { get; set; } = 2;

        /// <summary>
        /// This should be longer than the Azure Storage server-side timeouts (30 seconds).
        /// Source: https://learn.microsoft.com/en-us/rest/api/storageservices/setting-timeouts-for-table-service-operations
        /// Source: https://learn.microsoft.com/en-us/rest/api/storageservices/query-timeout-and-pagination
        /// </summary>
        public TimeSpan AzureServiceClientNetworkTimeout { get; set; } = TimeSpan.FromSeconds(35);

        public string UserManagedIdentityClientId { get; set; } = null;

        public StorageCredentialType GetStorageCredentialType()
        {
            if (UseDevelopmentStorage)
            {
                return StorageCredentialType.DevelopmentStorage;
            }
            else if (UseMemoryStorage)
            {
                return StorageCredentialType.MemoryStorage;
            }
            else if (StorageAccountName is not null)
            {
                if (UserManagedIdentityClientId is not null)
                {
                    return StorageCredentialType.UserAssignedManagedIdentityCredential;
                }
                else if (StorageAccessKey is not null)
                {
                    return StorageCredentialType.StorageAccessKey;
                }
                else if (StorageClientTenantId is not null && StorageClientApplicationId is not null)
                {
                    if (StorageClientCertificatePath is not null)
                    {
                        return StorageCredentialType.ClientCertificateCredentialFromPath;
                    }
                    else if (StorageClientCertificateKeyVault is not null
                             && StorageClientCertificateKeyVaultCertificateName is not null)
                    {
                        return StorageCredentialType.ClientCertificateCredentialFromKeyVault;
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"If the {nameof(StorageClientTenantId)} and {nameof(StorageClientApplicationId)} settings are set, " +
                            $"either the {nameof(StorageClientCertificatePath)} setting must be set " +
                            $"or the {nameof(StorageClientCertificateKeyVault)} and {nameof(StorageClientCertificateKeyVaultCertificateName)} settings must be. " +
                            $"This enables Entra ID service principal authentication using the configured client certificate.");
                    }
                }
                else
                {
                    return StorageCredentialType.DefaultAzureCredential;
                }
            }
            else
            {
                throw new ArgumentException(
                    $"Either the {nameof(StorageAccountName)} setting must be set, " +
                    $"or the {nameof(UseDevelopmentStorage)} setting must be set to true, " +
                    $"or the {nameof(UseMemoryStorage)} setting must be set to true. " +
                    $"Set {nameof(UseDevelopmentStorage)} to true if you want to use Azurite.");
            }
        }
    }
}
