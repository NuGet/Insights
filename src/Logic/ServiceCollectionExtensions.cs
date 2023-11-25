// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;
using Knapcode.MiniZip;
using NuGet.Configuration;
using NuGet.Insights.ReferenceTracking;
using NuGet.Insights.TablePrefixScan;
using NuGet.Insights.WideEntities;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Validation.PackageSigning.ValidateCertificate;

namespace NuGet.Insights
{
    public static class ServiceCollectionExtensions
    {
        private class Marker
        {
        }

        /// <summary>
        /// This should be longer than the Azure Storage server-side timeouts.
        /// Source: https://learn.microsoft.com/en-us/rest/api/storageservices/setting-timeouts-for-table-service-operations
        /// Source: https://learn.microsoft.com/en-us/rest/api/storageservices/query-timeout-and-pagination
        /// </summary>
        public static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(45);

        private static readonly SocketsHttpHandler DecompressingHandler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        private static readonly SocketsHttpHandler NoDecompressingHandler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        private class NoDisposeHandler : DelegatingHandler
        {
            protected override void Dispose(bool disposing)
            {
                // Ignore
            }
        }

        private static HttpMessageHandler GetHttpMessageHandler(IServiceProvider serviceProvider, bool enableLogging, bool automaticDecompression)
        {
            HttpMessageHandler pipeline = automaticDecompression ? DecompressingHandler : NoDecompressingHandler;

            // Protect against unintentional disposal
            pipeline = new NoDisposeHandler { InnerHandler = pipeline };

            // Enable a hook in the HTTP pipeline
            var factory = serviceProvider.GetService<INuGetInsightsHttpMessageHandlerFactory>();
            if (factory != null)
            {
                var handler = factory.Create();
                handler.InnerHandler = pipeline;
                pipeline = handler;
            }

            // Optionally enable logging
            var logger = serviceProvider.GetRequiredService<ILogger<LoggingHandler>>();
            if (enableLogging && (logger.IsEnabled(LogLevel.Warning) || logger.IsEnabled(LogLevel.Information)))
            {
                var handler = new LoggingHandler(logger) { InnerHandler = pipeline };
                pipeline = handler;
            }

            return pipeline;
        }

        private static HttpClient GetHttpClient(
            IServiceProvider serviceProvider,
            bool enableLogging,
            bool automaticDecompression)
        {
            var handler = GetHttpMessageHandler(serviceProvider, enableLogging, automaticDecompression);
            var httpClient = new HttpClient(handler) { Timeout = HttpClientTimeout };
            UserAgent.SetUserAgent(httpClient);
            return httpClient;
        }

        public static IServiceCollection AddNuGetInsights(
            this IServiceCollection serviceCollection,
            string programName = null,
            string programVersion = null,
            string programUrl = null)
        {
            // Avoid re-adding all the services.
            if (serviceCollection.Any(x => x.ServiceType == typeof(Marker)))
            {
                return serviceCollection;
            }

            serviceCollection.AddSingleton<Marker>();

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
                    if (AllowIcu())
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

            var userAgent = GetUserAgent(programName, programVersion, programUrl);

            // Set the user agent for NuGet Client HTTP requests (i.e. HttpSource)
            typeof(UserAgent)
                .GetProperty(nameof(UserAgent.UserAgentString), BindingFlags.Public | BindingFlags.Static | BindingFlags.SetProperty)
                .SetMethod
                .Invoke(null, new object[] { userAgent });

            serviceCollection.AddSingleton(x => GetHttpClient(x, enableLogging: true, automaticDecompression: true));

            serviceCollection.AddLogging(o =>
            {
                o.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            });

            serviceCollection.AddSingleton(x =>
            {
                var httpClient = GetHttpClient(x, enableLogging: true, automaticDecompression: false);
                return new ServiceClientFactory(
                    () => httpClient,
                    x.GetRequiredService<IOptions<NuGetInsightsSettings>>(),
                    x.GetRequiredService<ILoggerFactory>());
            });

            serviceCollection.AddSingleton<IThrottle>(NullThrottle.Instance);

            serviceCollection.AddSingleton(
                x =>
                {
                    var options = x.GetRequiredService<IOptions<NuGetInsightsSettings>>();
                    return new HttpSource(
                        new PackageSource(options.Value.V3ServiceIndex),
                        () =>
                        {
                            var handler = GetHttpMessageHandler(x, enableLogging: false, automaticDecompression: true);
                            var handlerResource = new HttpMessageHandlerResource(handler);
                            return Task.FromResult<HttpHandlerResource>(handlerResource);
                        },
                        x.GetRequiredService<IThrottle>());
                });

            serviceCollection.AddTransient(
                x => new HttpZipProvider(x.GetRequiredService<HttpClient>(), x.GetRequiredService<IThrottle>())
                {
                    RequireAcceptRanges = false,
                    BufferSizeProvider = new ZipBufferSizeProvider(
                        firstBufferSize: 1024 * 16,
                        secondBufferSize: 1024 * 16,
                        exponent: 2)
                });
            serviceCollection.AddTransient<MZipFormat>();
            serviceCollection.AddTransient<FileDownloader>();

            serviceCollection.AddTransient<SearchServiceCursorReader>();
            serviceCollection.AddTransient<IProgressReporter, NullProgressReporter>();
            serviceCollection.AddTransient<LatestCatalogCommitFetcher>();
            serviceCollection.AddTransient<StorageLeaseService>();
            serviceCollection.AddTransient<AutoRenewingStorageLeaseService>();
            serviceCollection.AddTransient<StorageSemaphoreLeaseService>();
            serviceCollection.AddTransient<TablePrefixScanner>();
            serviceCollection.AddTransient<ReferenceTracker>();
            serviceCollection.AddTransient<WideEntityService>();
            serviceCollection.AddTransient<PackageWideEntityService>();
            serviceCollection.AddTransient<PackageFileService>();
            serviceCollection.AddTransient<PackageHashService>();
            serviceCollection.AddTransient<PackageManifestService>();
            serviceCollection.AddTransient<PackageReadmeService>();
            serviceCollection.AddTransient<SymbolPackageFileService>();

            serviceCollection.AddSingleton<ITelemetryClient, TelemetryClientWrapper>();

            serviceCollection.AddTransient<TempStreamService>();
            serviceCollection.AddTransient<TempStreamWriter>();
            serviceCollection.AddScoped<TempStreamLeaseScope>();

            serviceCollection.AddTransient<V2Parser>();
            serviceCollection.AddSingleton<ServiceIndexCache>();
            serviceCollection.AddTransient<GalleryClient>();
            serviceCollection.AddTransient<V2Client>();
            serviceCollection.AddTransient<PackagesContainerClient>();
            serviceCollection.AddTransient<FlatContainerClient>();
            serviceCollection.AddTransient<RegistrationClient>();
            serviceCollection.AddTransient<SearchClient>();
            serviceCollection.AddTransient<BlobStorageJsonClient>();
            serviceCollection.AddTransient<PackageDownloadsClient>();
            serviceCollection.AddTransient<PackageOwnersClient>();
            serviceCollection.AddTransient<VerifiedPackagesClient>();
            serviceCollection.AddTransient<CatalogClient>();
            serviceCollection.AddSingleton<CatalogCommitTimestampProvider>();
            serviceCollection.AddTransient<IRemoteCursorClient, RemoteCursorClient>();

            serviceCollection.AddTransient<PackageConsistencyContextBuilder>();
            serviceCollection.AddTransient<GalleryConsistencyService>();
            serviceCollection.AddTransient<V2ConsistencyService>();
            serviceCollection.AddTransient<FlatContainerConsistencyService>();
            serviceCollection.AddTransient<PackagesContainerConsistencyService>();
            serviceCollection.AddTransient<RegistrationOriginalConsistencyService>();
            serviceCollection.AddTransient<RegistrationGzippedConsistencyService>();
            serviceCollection.AddTransient<RegistrationSemVer2ConsistencyService>();
            serviceCollection.AddTransient<SearchConsistencyService>();
            serviceCollection.AddTransient<PackageConsistencyService>();
            serviceCollection.AddTransient<CrossCheckConsistencyService>();

            serviceCollection.AddTransient<ICertificateVerifier, OnlineCertificateVerifier>();

            return serviceCollection;
        }

        private static string GetUserAgent(string programName, string programVersion, string programUrl)
        {
            var builder = new StringBuilder();

            // Ignored by the statistics pipeline here:
            // https://github.com/NuGet/NuGet.Jobs/blob/062f7501e34d34a12abf780f6a01629e66b7f28b/src/Stats.ImportAzureCdnStatistics/StatisticsParser.cs#L41
            builder.Append("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)");
            builder.Append(" ");

            // Parsed by the statistics pipeline here:
            // https://github.com/NuGet/NuGet.Jobs/blob/062f7501e34d34a12abf780f6a01629e66b7f28b/src/Stats.LogInterpretation/knownclients.yaml#L156-L158
            // Ignored by the statistics pipeline here:
            // https://github.com/NuGet/NuGet.Jobs/blob/062f7501e34d34a12abf780f6a01629e66b7f28b/src/Stats.Warehouse/Programmability/Functions/dbo.IsUnknownClient.sql#L19
            builder.Append(new UserAgentStringBuilder("NuGet Test Client")
                .WithOSDescription(RuntimeInformation.OSDescription)
                .Build());

            builder.Append(" ");
            if (string.IsNullOrWhiteSpace(programName))
            {
                builder.Append("NuGet.Insights");
            }
            else
            {
                builder.Append(programName);
            }
            builder.Append(".Bot");

            if (string.IsNullOrWhiteSpace(programVersion))
            {
                var assemblyInformationalVersion = typeof(ServiceCollectionExtensions)
                    .Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;
                if (assemblyInformationalVersion != null)
                {
                    builder.Append("/");
                    builder.Append(assemblyInformationalVersion);
                }
            }
            else
            {
                builder.Append("/");
                builder.Append(programVersion);
            }

            builder.Append(" (");
            builder.Append(RuntimeInformation.OSArchitecture);
            builder.Append("; ");
            builder.Append(RuntimeInformation.ProcessArchitecture);
            builder.Append("; ");
            builder.Append(RuntimeInformation.FrameworkDescription);
            builder.Append("; +");
            if (string.IsNullOrWhiteSpace(programUrl))
            {
                builder.Append("https://github.com/NuGet/Insights");
            }
            else
            {
                builder.Append(programUrl);
            }
            builder.Append(")");

            return builder.ToString();
        }

        private const string AllowIcuEnvName = "NUGET_INSIGHTS_ALLOW_ICU";

        private static bool AllowIcu()
        {
            var value = Environment.GetEnvironmentVariable(AllowIcuEnvName);
            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return false;
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
