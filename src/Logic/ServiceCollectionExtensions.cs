using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.TablePrefixScan;
using Knapcode.ExplorePackages.WideEntities;
using Knapcode.MiniZip;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages
{
    public static class ServiceCollectionExtensions
    {
        public const string HttpClientName = "Knapcode.ExplorePackages";
        public const string LoggingHttpClientName = "Knapcode.ExplorePackages.Logging";

        private static IHttpClientBuilder AddExplorePackages(this IHttpClientBuilder builder)
        {
            return builder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
                .AddHttpMessageHandler(serviceProvider =>
                {
                    // Enable a hook for injecting additional HTTP messages in.
                    var factory = serviceProvider.GetService<IExplorePackagesHttpMessageHandlerFactory>();
                    if (factory != null)
                    {
                        return factory.Create();
                    }

                    return new NullDelegatingHandler();
                })
                .AddHttpMessageHandler<UrlReporterHandler>()
                .ConfigureHttpClient(x =>
                {
                    if (x.DefaultRequestHeaders.UserAgent == null
                        || x.DefaultRequestHeaders.UserAgent.Count == 0)
                    {
                        UserAgent.SetUserAgent(x);
                    }
                });
        }

        public static IServiceCollection AddExplorePackages(
            this IServiceCollection serviceCollection,
            string programName = null,
            string programVersion = null,
            string programUrl = null)
        {
            serviceCollection.AddMemoryCache();

            var userAgent = GetUserAgent(programName, programVersion, programUrl);

            // Set the user agent for NuGet Client HTTP requests (i.e. HttpSource)
            typeof(UserAgent)
                .GetProperty(nameof(UserAgent.UserAgentString), BindingFlags.Public | BindingFlags.Static | BindingFlags.SetProperty)
                .SetMethod
                .Invoke(null, new object[] { userAgent });

            serviceCollection
                .AddHttpClient(HttpClientName)
                .AddExplorePackages();

            serviceCollection
                .AddHttpClient(LoggingHttpClientName)
                .AddExplorePackages()
                .AddHttpMessageHandler<LoggingHandler>();

            serviceCollection.AddTransient(x => x
                .GetRequiredService<IHttpClientFactory>()
                .CreateClient(LoggingHttpClientName));

            serviceCollection.AddLogging(o =>
            {
                o.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            });

            serviceCollection.AddSingleton<ServiceClientFactory>();
            serviceCollection.AddAzureClientsCore();
            serviceCollection.AddSingleton<AzureLoggingStartup>();

            serviceCollection.AddSingleton<IThrottle>(NullThrottle.Instance);

            serviceCollection.AddSingleton<UrlReporterProvider>();
            serviceCollection.AddTransient<UrlReporterHandler>();
            serviceCollection.AddTransient<LoggingHandler>();
            serviceCollection.AddTransient(
                x =>
                {
                    var options = x.GetRequiredService<IOptions<ExplorePackagesSettings>>();
                    return new HttpSource(
                        new PackageSource(options.Value.V3ServiceIndex),
                        () =>
                        {
                            var factory = x.GetRequiredService<IHttpMessageHandlerFactory>();
                            var httpMessageHandler = factory.CreateHandler(HttpClientName);

                            return Task.FromResult<HttpHandlerResource>(new HttpMessageHandlerResource(httpMessageHandler));
                        },
                        x.GetRequiredService<IThrottle>());
                });

            serviceCollection.AddTransient(
                x => new HttpZipProvider(x.GetRequiredService<HttpClient>(), x.GetRequiredService<IThrottle>())
                {
                    BufferSizeProvider = new ZipBufferSizeProvider(
                        firstBufferSize: 1024 * 16,
                        secondBufferSize: 1024 * 16,
                        exponent: 2)
                });
            serviceCollection.AddTransient<MZipFormat>();

            serviceCollection.AddTransient<SearchServiceCursorReader>();
            serviceCollection.AddTransient<IProgressReporter, NullProgressReporter>();
            serviceCollection.AddTransient<LatestCatalogCommitFetcher>();
            serviceCollection.AddTransient<StorageLeaseService>();
            serviceCollection.AddTransient<AutoRenewingStorageLeaseService>();
            serviceCollection.AddTransient<StorageSemaphoreLeaseService>();
            serviceCollection.AddTransient<TablePrefixScanner>();
            serviceCollection.AddTransient<WideEntityService>();
            serviceCollection.AddTransient<PackageWideEntityService>();
            serviceCollection.AddTransient<PackageFileService>();
            serviceCollection.AddTransient<PackageHashService>();
            serviceCollection.AddTransient<PackageManifestService>();

            serviceCollection.AddSingleton<ITelemetryClient>(s => NullTelemetryClient.Instance);

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
            serviceCollection.AddTransient<IPackageDownloadsClient, PackageDownloadsClient>();
            serviceCollection.AddTransient<DownloadsV1JsonDeserializer>();
            serviceCollection.AddTransient<PackageOwnersClient>();
            serviceCollection.AddTransient<OwnersV2JsonDeserializer>();
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
                builder.Append("Knapcode.ExplorePackages");
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
                builder.Append("https://github.com/joelverhagen/ExplorePackages");
            }
            else
            {
                builder.Append(programUrl);
            }
            builder.Append(")");

            return builder.ToString();
        }
    }
}
