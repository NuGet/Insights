// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Kusto.Cloud.Platform.Utils;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using NuGet.Insights.Worker;

#nullable enable

namespace NuGet.Insights.Kusto
{
    public class CachingKustoClientFactory : IDisposable
    {
        private static readonly object TraceListenersLock = new object();
        private static bool AddedTraceListener = false;

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private ServiceClients? _serviceClients;

        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILoggerFactory _loggerFactory;

        public CachingKustoClientFactory(
            IOptions<NuGetInsightsWorkerSettings> options,
            ILoggerFactory loggerFactory)
        {
            _options = options;
            _loggerFactory = loggerFactory;

            SetupTraceListener();
        }

        public virtual async Task<ICslAdminProvider> GetAdminClientAsync()
        {
            return (await GetCachedServiceClientsAsync()).AdminClient;
        }

        public virtual async Task<ICslQueryProvider> GetQueryClientAsync()
        {
            return (await GetCachedServiceClientsAsync()).QueryClient;
        }

        public virtual async Task<IKustoQueuedIngestClient> GetIngestClientAsync()
        {
            return (await GetCachedServiceClientsAsync()).IngestClient;
        }

        private void SetupTraceListener()
        {
            if (_options.Value.EnableDiagnosticTracingToLogger && !AddedTraceListener)
            {
                lock (TraceListenersLock)
                {
                    var anyListener = Trace
                        .Listeners
                        .OfType<LoggerTraceListener>()
                        .Any();

                    if (!anyListener)
                    {
                        ContextAwareTraceFormatter.GetStaticContext(
                            out _,
                            out var machineName,
                            out var instanceId,
                            out var instanceNumericId);
                        ContextAwareTraceFormatter.SetStaticContext(
                            "KustoClientSideTracing",
                            machineName,
                            instanceId,
                            instanceNumericId);

                        Trace.Listeners.Add(new LoggerTraceListener(_loggerFactory));
                        AddedTraceListener = true;
                    }
                }
            }
        }

        private async Task<ServiceClients> GetCachedServiceClientsAsync(CancellationToken token = default)
        {
            if (TryGetServiceClients(out var serviceClients))
            {
                return serviceClients;
            }

            await _lock.WaitAsync(token);
            try
            {
                if (TryGetServiceClients(out serviceClients))
                {
                    return serviceClients;
                }

                _serviceClients = await GetServiceClientsAsync(
                    created: DateTimeOffset.UtcNow,
                    _options.Value,
                    _loggerFactory);

                return _serviceClients;
            }
            finally
            {
                _lock.Release();
            }
        }

        private bool TryGetServiceClients([NotNullWhen(true)] out ServiceClients? serviceClients)
        {
            serviceClients = _serviceClients;
            if (serviceClients is null)
            {
                return false;
            }

            // We cache the instances forever, for now. Otherwise we would have to handle rented instances much like
            // HttpClientFactory. It should be possible by wrapping the clients in a disposable handle type, but we
            // can do that later.

            return true;
        }

        private static async Task<ServiceClients> GetServiceClientsAsync(
            DateTimeOffset created,
            NuGetInsightsWorkerSettings settings,
            ILoggerFactory loggerFactory)
        {
            var mainConnectionStringBuilder = await GetKustoConnectionStringBuilderAsync(addIngest: false, settings, loggerFactory);

            var adminProvider = KustoClientFactory.CreateCslAdminProvider(mainConnectionStringBuilder);
            if (adminProvider is null)
            {
                throw new InvalidOperationException($"The {nameof(ICslAdminProvider)} instance must not be null, even if there is no Kusto configuration.");
            }

            var queryProvider = KustoClientFactory.CreateCslQueryProvider(mainConnectionStringBuilder);
            if (queryProvider is null)
            {
                throw new InvalidOperationException($"The {nameof(ICslQueryProvider)} instance must not be null, even if there is no Kusto configuration.");
            }

            var ingestConnectionStringBuilder = await GetKustoConnectionStringBuilderAsync(addIngest: true, settings, loggerFactory);

            var ingestClient = KustoIngestFactory.CreateQueuedIngestClient(ingestConnectionStringBuilder);
            if (ingestClient is null)
            {
                throw new InvalidOperationException($"The {nameof(IKustoQueuedIngestClient)} instance must not be null, even if there is no Kusto configuration.");
            }

            return new ServiceClients(
                created,
                adminProvider,
                queryProvider,
                ingestClient);
        }

        public static async Task<KustoConnectionStringBuilder> GetKustoConnectionStringBuilderAsync(
            bool addIngest,
            NuGetInsightsWorkerSettings settings,
            ILoggerFactory loggerFactory)
        {
            if (string.IsNullOrEmpty(settings.KustoConnectionString))
            {
                return new KustoConnectionStringBuilder("https://localhost:8080");
            }

            var builder = new KustoConnectionStringBuilder(settings.KustoConnectionString);

            if (settings.KustoClientCertificatePath != null)
            {
                var certificate = new X509Certificate2(settings.KustoClientCertificatePath);
                builder = builder.WithAadApplicationCertificateAuthentication(
                    builder.ApplicationClientId,
                    certificate,
                    builder.Authority,
                    builder.ApplicationCertificateSendX5c);
            }
            else if (settings.KustoClientCertificateKeyVault != null)
            {
                var tokenCredential = CachingTokenCredential.MaybeWrap(
                    CredentialUtility.GetDefaultAzureCredential(),
                    loggerFactory,
                    settings,
                    builder.Authority);
                var secretReader = new SecretClient(
                    new Uri(settings.KustoClientCertificateKeyVault),
                    tokenCredential);
                KeyVaultSecret certificateContent = await secretReader.GetSecretAsync(
                    settings.KustoClientCertificateKeyVaultCertificateName);
                var certificateBytes = Convert.FromBase64String(certificateContent.Value);
                var certificate = new X509Certificate2(certificateBytes);
                builder = builder.WithAadApplicationCertificateAuthentication(
                    builder.ApplicationClientId,
                    certificate,
                    builder.Authority,
                    builder.ApplicationCertificateSendX5c);
            }
            else if (settings.UserManagedIdentityClientId != null && settings.KustoUseUserManagedIdentity)
            {
                builder = builder.WithAadUserManagedIdentity(settings.UserManagedIdentityClientId);
            }
            else
            {
                var tokenCredential = CachingTokenCredential.MaybeWrap(
                    CredentialUtility.GetDefaultAzureCredential(),
                    loggerFactory,
                    settings,
                    builder.Authority);
                builder = builder.WithAadAzureTokenCredentialsAuthentication(tokenCredential);
            }

            const string prefix = "https://";
            if (builder.DataSource == null || !builder.DataSource.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The Kusto connection must have a data source that starts with '{prefix}'.");
            }

            if (addIngest)
            {
                builder.DataSource = prefix + "ingest-" + builder.DataSource.Substring(prefix.Length);
            }

            return builder;
        }

        public void Dispose()
        {
            _lock.Wait();
            try
            {
                _serviceClients?.Dispose();
                _serviceClients = null;
            }
            finally
            {
                _lock.Release();
            }
        }

        private record ServiceClients(
            DateTimeOffset Created,
            ICslAdminProvider AdminClient,
            ICslQueryProvider QueryClient,
            IKustoQueuedIngestClient IngestClient) : IDisposable
        {
            public void Dispose()
            {
                AdminClient.Dispose();
                QueryClient.Dispose();
                IngestClient.Dispose();
            }
        }
    }
}
