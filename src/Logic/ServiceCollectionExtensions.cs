// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;
using Knapcode.MiniZip;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using NuGet.Insights.ReferenceTracking;
using NuGet.Insights.TablePrefixScan;
using NuGet.Insights.WideEntities;
using NuGet.Protocol.Core.Types;
using Validation.PackageSigning.ValidateCertificate;

namespace NuGet.Insights
{
    public static class ServiceCollectionExtensions
    {
        private class Marker
        {
        }

        private const string DefaultHttpClient = "NuGet.Insights.HttpClient.Decompressing";
        private const string ServiceClientHttpClient = "NuGet.Insights.HttpClient.ServiceClient";

        public static IServiceCollection AddNuGetInsights(
            this IServiceCollection serviceCollection,
            IConfiguration configuration = null,
            string userAgentAppName = null)
        {
            // Avoid re-adding all the services.
            if (serviceCollection.Any(x => x.ServiceType == typeof(Marker)))
            {
                return serviceCollection;
            }

            serviceCollection.AddSingleton<Marker>();

            if (configuration is null)
            {
                configuration = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .Build();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string error = null;

                if (!HasNlsInvariantBehavior())
                {
                    error = $"The behavior of {nameof(string.ToLowerInvariant)} is not expected. Check for breaking changes in {nameof(CultureInfo.InvariantCulture)}.";
                }

                if (IcuMode())
                {
                    error = "ICU mode is not supported on Windows. NLS must be used. See https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu#use-nls-instead-of-icu.";
                }

                if (error is not null)
                {
                    if (AllowIcu(configuration))
                    {
                        Console.WriteLine($"Environment {AllowIcuEnvName} has been set to true so the following error is suppressed:" + Environment.NewLine + error);
                    }
                    else
                    {
                        throw new NotSupportedException(error);
                    }
                }
            }

            serviceCollection.AddMemoryCache();

            var userAgent = GetUserAgent(configuration, userAgentAppName);

            // Set the user agent for NuGet Client HTTP requests (i.e. HttpSource)
            typeof(UserAgent)
                .GetProperty(nameof(UserAgent.UserAgentString), BindingFlags.Public | BindingFlags.Static | BindingFlags.SetProperty)
                .SetMethod
                .Invoke(null, [userAgent]);

            serviceCollection.AddHttpClient(
                DefaultHttpClient,
                allowAutoRedirect: true,
                decompressionMethods: DecompressionMethods.All,
                addRetryPolicy: true,
                enableLogging: x => x.GetRequiredService<ILogger<LoggingHttpHandler>>().IsEnabled(LogLevel.Information),
                addTimeoutPolicy: true);

            serviceCollection.AddHttpClient(
                ServiceClientHttpClient,
                allowAutoRedirect: false,
                decompressionMethods: DecompressionMethods.None,
                addRetryPolicy: false,
                enableLogging: x => x.GetRequiredService<ILogger<ServiceClientFactory>>().IsEnabled(LogLevel.Debug),
                addTimeoutPolicy: false);

            serviceCollection.AddSingleton<Func<HttpClient>>(provider =>
            {
                var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                return () => httpClientFactory.CreateClient(DefaultHttpClient);
            });

            serviceCollection.AddSingleton<RedirectResolver>();

            serviceCollection.AddLogging(o =>
            {
                o.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            });

            serviceCollection.AddSingleton(provider =>
            {
                var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                return new ServiceClientFactory(
                    () => httpClientFactory.CreateClient(ServiceClientHttpClient),
                    provider.GetRequiredService<IOptions<NuGetInsightsSettings>>(),
                    provider.GetRequiredService<ITelemetryClient>(),
                    provider.GetRequiredService<ILoggerFactory>());
            });

            serviceCollection.AddSingleton<IThrottle>(NullThrottle.Instance);

            serviceCollection.AddSingleton(x => TimeProvider.System);

            serviceCollection.AddSingleton<Func<HttpZipProvider>>(
                x => () => new HttpZipProvider(x.GetRequiredService<Func<HttpClient>>()(), x.GetRequiredService<IThrottle>())
                {
                    RequireAcceptRanges = false,
                    BufferSizeProvider = new ZipBufferSizeProvider(
                        firstBufferSize: 1024 * 16,
                        secondBufferSize: 1024 * 16,
                        exponent: 2)
                });
            serviceCollection.AddSingleton<MZipFormat>();
            serviceCollection.AddSingleton<FileDownloader>();

            serviceCollection.AddSingleton<StorageLeaseService>();
            serviceCollection.AddSingleton<AutoRenewingStorageLeaseService>();
            serviceCollection.AddSingleton<StorageSemaphoreLeaseService>();
            serviceCollection.AddSingleton<TablePrefixScanner>();
            serviceCollection.AddSingleton<ReferenceTracker>();
            serviceCollection.AddSingleton<WideEntityService>();
            serviceCollection.AddSingleton<PackageWideEntityService>();
            serviceCollection.AddSingleton<PackageFileService>();
            serviceCollection.AddSingleton<PackageHashService>();
            serviceCollection.AddSingleton<SymbolPackageHashService>();
            serviceCollection.AddSingleton<PackageManifestService>();
            serviceCollection.AddSingleton<PackageReadmeService>();
            serviceCollection.AddSingleton<SymbolPackageFileService>();
            serviceCollection.AddSingleton<SymbolPackageClient>();

            serviceCollection.AddSingleton(typeof(EntityUpsertStorageService<,>));

            serviceCollection.AddSingleton<ITelemetryClient, TelemetryClientWrapper>();

            serviceCollection.AddSingleton<TempStreamService>();
            serviceCollection.AddSingleton<TempStreamDirectoryLeaseService>();
            serviceCollection.AddSingleton<Func<TempStreamWriter>>(x => () => new TempStreamWriter(
                x.GetRequiredService<TempStreamDirectoryLeaseService>(),
                x.GetRequiredService<IOptions<NuGetInsightsSettings>>(),
                x.GetRequiredService<ILogger<TempStreamWriter>>()));

            serviceCollection.AddSingleton<ServiceIndexCache>();
            serviceCollection.AddSingleton<FlatContainerClient>();
            serviceCollection.AddSingleton<ExternalBlobStorageClient>();
            serviceCollection.AddSingleton<PackageDownloadsClient>();
            serviceCollection.AddSingleton<PackageOwnersClient>();
            serviceCollection.AddSingleton<VerifiedPackagesClient>();
            serviceCollection.AddSingleton<ExcludedPackagesClient>();
            serviceCollection.AddSingleton<PopularityTransfersClient>();
            serviceCollection.AddSingleton<GitHubUsageClient>();
            serviceCollection.AddSingleton<CatalogClient>();
            serviceCollection.AddSingleton<CatalogCommitTimestampProvider>();
            serviceCollection.AddSingleton<IRemoteCursorClient, RemoteCursorClient>();

            serviceCollection.AddSingleton<ICertificateVerifier, OnlineCertificateVerifier>();

            return serviceCollection;
        }

        private static string GetUserAgent(IConfiguration configuration, string appName)
        {
            var builder = new StringBuilder();

            // Ignored by the statistics pipeline here:
            // https://github.com/NuGet/NuGet.Jobs/blob/062f7501e34d34a12abf780f6a01629e66b7f28b/src/Stats.ImportAzureCdnStatistics/StatisticsParser.cs#L41
            builder.Append("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)");
            builder.Append(' ');

            // Parsed by the statistics pipeline here:
            // https://github.com/NuGet/NuGet.Jobs/blob/062f7501e34d34a12abf780f6a01629e66b7f28b/src/Stats.LogInterpretation/knownclients.yaml#L156-L158
            // Ignored by the statistics pipeline here:
            // https://github.com/NuGet/NuGet.Jobs/blob/062f7501e34d34a12abf780f6a01629e66b7f28b/src/Stats.Warehouse/Programmability/Functions/dbo.IsUnknownClient.sql#L19
            builder.Append(new UserAgentStringBuilder("NuGet Test Client")
                .WithOSDescription(RuntimeInformation.OSDescription)
                .Build());

            builder.Append(' ');
            if (string.IsNullOrWhiteSpace(appName))
            {
                builder.Append(Assembly.GetEntryAssembly()?.GetName().Name ?? "NuGet.Insights");
            }
            else
            {
                builder.Append(appName);
            }
            builder.Append(".Bot");

            var assemblyInformationalVersion = typeof(ServiceCollectionExtensions)
                .Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (assemblyInformationalVersion != null)
            {
                builder.Append('/');
                builder.Append(assemblyInformationalVersion);
            }

            builder.Append(" (");
            builder.Append(RuntimeInformation.OSArchitecture);
            builder.Append("; ");
            builder.Append(RuntimeInformation.ProcessArchitecture);
            builder.Append("; ");
            builder.Append(RuntimeInformation.FrameworkDescription);
            builder.Append("; +");
            builder.Append(GetUserAgentInfoUrl(configuration));
            builder.Append(')');

            return builder.ToString();
        }

        private const string AllowIcuEnvName = "NUGET_INSIGHTS_ALLOW_ICU";
        private const string UserAgentInfoUrlEnvName = "NUGET_INSIGHTS_USER_AGENT_INFO_URL";

        private static bool AllowIcu(IConfiguration configuration)
        {
            var value = configuration?[AllowIcuEnvName];
            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return false;
        }

        private static void AddHttpClient(
            this IServiceCollection serviceCollection,
            string name,
            bool allowAutoRedirect,
            DecompressionMethods decompressionMethods,
            bool addRetryPolicy,
            Func<IServiceProvider, bool> enableLogging,
            bool addTimeoutPolicy)
        {
            var builder = serviceCollection
                .AddHttpClient(name)
                .RemoveAllLoggers()
                .ConfigureHttpClient(httpClient =>
                {
                    // lower timeout is either handled by the timeout policy (internally) or the Azure service client (externally).
                    httpClient.Timeout = TimeSpan.FromMinutes(5);
                    UserAgent.SetUserAgent(httpClient);
                });

            if (addRetryPolicy)
            {
                builder
                    .AddHttpMessageHandler(provider =>
                    {
                        return new RetryHttpMessageHandler(
                            provider.GetRequiredService<IOptions<NuGetInsightsSettings>>(),
                            provider.GetRequiredService<ITelemetryClient>(),
                            provider.GetRequiredService<ILogger<RetryHttpMessageHandler>>());
                    });
            }

            builder.Services.Configure<HttpClientFactoryOptions>(
                builder.Name,
                options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(builder =>
                    {
                        if (enableLogging(builder.Services))
                        {
                            builder.AdditionalHandlers.Add(new LoggingHttpHandler(
                                builder.Services.GetRequiredService<ILogger<LoggingHttpHandler>>()));
                        }
                    });
                });

            builder
                .AddHttpMessageHandler(provider =>
                {
                    var telemetryClient = provider.GetRequiredService<ITelemetryClient>();
                    return new TelemetryHttpHandler(telemetryClient);
                });

            if (addTimeoutPolicy)
            {
                builder
                    .AddHttpMessageHandler(provider =>
                    {
                        return new TimeoutHttpMessageHandler(
                            provider.GetRequiredService<IOptions<NuGetInsightsSettings>>());
                    });
            }

            builder.Services.Configure<HttpClientFactoryOptions>(
                builder.Name,
                options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(builder =>
                    {
                        var factory = builder.Services.GetService<INuGetInsightsHttpMessageHandlerFactory>();
                        if (factory is not null)
                        {
                            builder.AdditionalHandlers.Add(factory.Create());
                        }
                    });
                });

            builder
                .ConfigurePrimaryHttpMessageHandler(provider =>
                {
                    return new SocketsHttpHandler
                    {
                        AllowAutoRedirect = allowAutoRedirect,
                        UseCookies = false,
                        AutomaticDecompression = decompressionMethods,
                    };
                });
        }

        private static string GetUserAgentInfoUrl(IConfiguration configuration)
        {
            var value = configuration?[UserAgentInfoUrlEnvName];
            if (string.IsNullOrWhiteSpace(value))
            {
                return "https://github.com/NuGet/Insights";
            }

            return value.Trim();
        }

        /// <summary>
        /// Source: https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu#determine-if-your-app-is-using-icu
        /// </summary>
        internal static bool IcuMode()
        {
            SortVersion sortVersion = CultureInfo.InvariantCulture.CompareInfo.Version;
            byte[] bytes = sortVersion.SortId.ToByteArray();
            int version = bytes[3] << 24 | bytes[2] << 16 | bytes[1] << 8 | bytes[0];
            return version != 0 && version == sortVersion.FullVersion;
        }

        internal static bool HasNlsInvariantBehavior()
        {
            // This is the lowest value Unicode character where ToLowerVariant behaves differently on ICU vs. NLS.
            return "ǅ".ToLowerInvariant() == "ǅ";
        }
    }
}
