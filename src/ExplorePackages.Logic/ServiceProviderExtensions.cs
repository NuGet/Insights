using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages.Logic
{
    public static class ServiceProviderExtensions
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Converters =
            {
                new StringEnumConverter(),
            },
            Formatting = Formatting.Indented,
        };

        public static async Task InitializeGlobalStateAsync<TLogger>(
            this IServiceProvider serviceProvider,
            bool initializeDatabase)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<TLogger>>();
            logger.LogInformation("===== initialize =====");

            // Log the settings
            using (var loggingScope = serviceProvider.CreateScope())
            {
                loggingScope.ServiceProvider.SanitizeAndLogSettings();
            }

            // Initialize the database.
            if (initializeDatabase)
            {
                using (var entityContext = serviceProvider.GetRequiredService<IEntityContext>())
                {
                    await entityContext.Database.MigrateAsync();
                    logger.LogInformation("The database schema is up to date.");
                }
            }
            else
            {
                logger.LogInformation("The database will not be used.");
            }

            // Set the user agent.
            var userAgentStringBuilder = new UserAgentStringBuilder("Knapcode.ExplorePackages.Bot");
            UserAgent.SetUserAgentString(userAgentStringBuilder);
            logger.LogInformation("The following user agent will be used: {UserAgent}", UserAgent.UserAgentString);

            // Allow many concurrent outgoing connections.
            ServicePointManager.DefaultConnectionLimit = 64;

            logger.LogInformation("======================" + Environment.NewLine);
        }

        public static void SanitizeAndLogSettings(this IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ExplorePackagesSettings>>();
            var options = serviceProvider.GetRequiredService<IOptionsSnapshot<ExplorePackagesSettings>>();
            var settings = options.Value;

            logger.LogInformation("===== settings =====");

            // Sanitize the DB connection string
            settings.DatabaseConnectionString = Regex.Replace(
                settings.DatabaseConnectionString,
                "(User ID|UID|Password|PWD)=[^;]*",
                "$1=(redacted)",
                RegexOptions.IgnoreCase);

            // Sanitize the Azure Blob Storage connection strings
            settings.StorageConnectionString = Regex.Replace(
                settings.StorageConnectionString,
                "(SharedAccessSignature|AccountKey)=[^;]*",
                "$1=(redacted)",
                RegexOptions.IgnoreCase);
            settings.LatestPackageLeavesStorageConnectionString = Regex.Replace(
                settings.LatestPackageLeavesStorageConnectionString,
                "(SharedAccessSignature|AccountKey)=[^;]*",
                "$1=(redacted)",
                RegexOptions.IgnoreCase);

            logger.LogInformation(JsonConvert.SerializeObject(settings, SerializerSettings));

            logger.LogInformation("====================" + Environment.NewLine);
        }
    }
}
