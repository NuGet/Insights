// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class StorageSettings
    {
        public bool UseDevelopmentStorage { get; set; } = false;

        public string StorageAccountName { get; set; } = null;
        public string StorageClientApplicationId { get; set; } = null;
        public string StorageClientTenantId { get; set; } = null;
        public string StorageClientCertificatePath { get; set; } = null;
        public string StorageClientCertificateKeyVault { get; set; } = null;
        public string StorageClientCertificateKeyVaultCertificateName { get; set; } = null;
        public string StorageAccessKey { get; set; } = null;

        public TimeSpan ServiceClientRefreshPeriod { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan ServiceClientSasDuration { get; set; } = TimeSpan.FromHours(12);

        public int AzureServiceClientMaxRetries { get; set; } = 2;

        /// <summary>
        /// This should be longer than the Azure Storage server-side timeouts (30 seconds).
        /// Source: https://learn.microsoft.com/en-us/rest/api/storageservices/setting-timeouts-for-table-service-operations
        /// Source: https://learn.microsoft.com/en-us/rest/api/storageservices/query-timeout-and-pagination
        /// </summary>
        public TimeSpan AzureServiceClientNetworkTimeout { get; set; } = TimeSpan.FromSeconds(35);

        public string UserManagedIdentityClientId { get; set; } = null;
    }
}
