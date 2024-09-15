// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class StorageAccountSettings
    {
        public string StorageAccountName { get; set; } = null;
        public string StorageClientApplicationId { get; set; } = null;
        public string StorageClientTenantId { get; set; } = null;
        public string StorageClientCertificatePath { get; set; } = null;
        public string StorageClientCertificateKeyVault { get; set; } = null;
        public string StorageClientCertificateKeyVaultCertificateName { get; set; } = null;
        public string StorageBlobReadSharedAccessSignature { get; set; } = null;
        public string StorageConnectionString { get; set; } = null;
        public TimeSpan ServiceClientRefreshPeriod { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan ServiceClientSasDuration { get; set; } = TimeSpan.FromHours(12);
    }
}
