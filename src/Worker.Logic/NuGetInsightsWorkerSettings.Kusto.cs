// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings
    {
        public TimeSpan KustoBlobIngestionTimeout { get; set; } = TimeSpan.FromHours(6);

        public string KustoIngestionTableName { get; set; } = "kustoingestions";

        public string KustoConnectionString { get; set; } = null;
        public string KustoDatabaseName { get; set; } = null;
        public string KustoClientCertificateKeyVault { get; set; } = null;
        public string KustoClientCertificateKeyVaultCertificateName { get; set; } = null;
        public bool KustoUseUserManagedIdentity { get; set; } = true;

        /// <summary>
        /// A path to a certificate that will be loaded as a <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2"/>
        /// for Kusto AAD client app authentication.
        /// </summary>
        public string KustoClientCertificatePath { get; set; } = null;

        public bool KustoApplyPartitioningPolicy { get; set; } = true;
        public string KustoTableNameFormat { get; set; } = "{0}";
        public string KustoTableFolder { get; set; } = string.Empty;
        public string KustoTableDocstringFormat { get; set; } = "See https://github.com/NuGet/Insights/blob/main/docs/tables/{0}.md";
        public string KustoTempTableNameFormat { get; set; } = "{0}_Temp";
        public string KustoOldTableNameFormat { get; set; } = "{0}_Old";
        public int OldKustoIngestionsToKeep { get; set; } = 9;
        public int KustoIngestionMaxAttempts { get; set; } = 10;
        public int KustoValidationMaxAttempts { get; set; } = 3;
    }
}
