using System;
using System.Text.RegularExpressions;
using Knapcode.ExplorePackages.Logic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Knapcode.ExplorePackages.Tool
{
    public static class ExtensionMethods
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Converters =
            {
                new StringEnumConverter(),
            },
            Formatting = Formatting.Indented,
        };

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

            // Sanitize the Azure Blob Storage connection string
            settings.StorageConnectionString = Regex.Replace(
                settings.StorageConnectionString,
                "(SharedAccessSignature|AccountKey)=[^;]*",
                "$1=(redacted)",
                RegexOptions.IgnoreCase);

            logger.LogInformation(JsonConvert.SerializeObject(settings, SerializerSettings));

            logger.LogInformation("====================" + Environment.NewLine);
        }
    }
}
