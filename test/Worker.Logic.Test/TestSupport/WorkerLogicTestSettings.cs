// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public static class WorkerLogicTestSettings
    {
        private const string KustoConnectionStringEnv = "NUGETINSIGHTS_KUSTOCONNECTIONSTRING";
        private const string KustoDatabaseNameEnv = "NUGETINSIGHTS_KUSTODATABASENAME";
        private const string KustoClientCertificatePathEnv = "NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEPATH";
        private const string KustoClientCertificateKeyVaultEnv = "NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEKEYVAULT";
        private const string KustoClientCertificateKeyVaultCertificateNameEnv = "NUGETINSIGHTS_KUSTOCLIENTCERTIFICATEKEYVAULTCERTIFICATENAME";

        private static string KustoConnectionString => LogicTestSettings.GetEnvOrNull(KustoConnectionStringEnv);
        private static string KustoDatabaseName => LogicTestSettings.GetEnvOrNull(KustoDatabaseNameEnv);
        private static string KustoClientCertificatePath => LogicTestSettings.GetEnvOrNull(KustoClientCertificatePathEnv);
        private static string KustoClientCertificateKeyVault => LogicTestSettings.GetEnvOrNull(KustoClientCertificateKeyVaultEnv);
        private static string KustoClientCertificateKeyVaultCertificateName => LogicTestSettings.GetEnvOrNull(KustoClientCertificateKeyVaultCertificateNameEnv);

        public static T WithTestKustoSettings<T>(this T settings) where T : NuGetInsightsWorkerSettings
        {
            settings.KustoConnectionString = KustoConnectionString;
            settings.KustoDatabaseName = KustoDatabaseName;
            settings.KustoClientCertificatePath = KustoClientCertificatePath;
            settings.KustoClientCertificateKeyVault = KustoClientCertificateKeyVault;
            settings.KustoClientCertificateKeyVaultCertificateName = KustoClientCertificateKeyVaultCertificateName;
            return settings;
        }
    }
}
