// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace NuGet.Insights
{
    public static class CredentialCache
    {
        private static readonly Lazy<DefaultAzureCredential> LazyDefaultAzureCredential = new Lazy<DefaultAzureCredential>(
            () => new DefaultAzureCredential());

        private static readonly ConcurrentDictionary<(string KeyVault, string CertificateName), Lazy<X509Certificate2>> KeyVaultCertificates = new();

        private static readonly ConcurrentDictionary<(string KeyVault, string CertificateName), Lazy<Task<X509Certificate2>>> AsyncKeyVaultCertificates = new();

        private static readonly ConcurrentDictionary<(string TenantId, string ApplicationId), Lazy<Task<ClientCertificateCredential>>> ClientCertificateCredentials = new();

        public static DefaultAzureCredential DefaultAzureCredential => LazyDefaultAzureCredential.Value;

        public static Lazy<X509Certificate2> GetLazyCertificate(string keyVault, string certificateName)
        {
            return KeyVaultCertificates.GetOrAdd(
                (keyVault, certificateName),
                new Lazy<X509Certificate2>(() =>
                {
                    var asyncLazy = GetLazyCertificateTask(keyVault, certificateName);
                    if (asyncLazy.IsValueCreated && asyncLazy.Value.IsCompletedSuccessfully)
                    {
                        return asyncLazy.Value.Result;
                    }

                    var secretReader = new SecretClient(
                        new Uri(keyVault),
                        DefaultAzureCredential);

                    KeyVaultSecret certificateContent = secretReader.GetSecret(certificateName);

                    var certificateBytes = Convert.FromBase64String(certificateContent.Value);

                    var certificate = new X509Certificate2(certificateBytes);

                    return certificate;
                }));
        }

        public static Lazy<Task<X509Certificate2>> GetLazyCertificateTask(string keyVault, string certificateName)
        {
            return AsyncKeyVaultCertificates.GetOrAdd(
                (keyVault, certificateName),
                new Lazy<Task<X509Certificate2>>(async () =>
                {
                    var syncLazy = GetLazyCertificate(keyVault, certificateName);
                    if (syncLazy.IsValueCreated)
                    {
                        return syncLazy.Value;
                    }

                    var secretReader = new SecretClient(
                        new Uri(keyVault),
                        DefaultAzureCredential);

                    KeyVaultSecret certificateContent = await secretReader.GetSecretAsync(certificateName);

                    var certificateBytes = Convert.FromBase64String(certificateContent.Value);

                    var certificate = new X509Certificate2(certificateBytes);

                    return certificate;
                }));
        }

        public static Lazy<Task<ClientCertificateCredential>> GetLazyClientCertificateCredentialTask(
            string tenantId,
            string applicationId,
            Func<Task<X509Certificate2>> getCertificateAsync)
        {
            return ClientCertificateCredentials.GetOrAdd(
                (tenantId, applicationId),
                new Lazy<Task<ClientCertificateCredential>>(async () =>
                {
                    var certificate = await getCertificateAsync();

                    return new ClientCertificateCredential(
                        tenantId,
                        applicationId,
                        certificate,
                        new ClientCertificateCredentialOptions { SendCertificateChain = true });
                }));
        }
    }
}
