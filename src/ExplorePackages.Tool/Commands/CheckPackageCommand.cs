using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Tool
{
    public class CheckPackageCommand : ICommand
    {
        private readonly PackageService _packageService;
        private readonly PackageConsistencyService _consistencyService;
        private readonly PackageConsistencyContextBuilder _contextBuilder;
        private readonly ILogger<CheckPackageCommand> _logger;

        private CommandArgument _idArgument;
        private CommandArgument _versionArgument;
        private CommandOption _semVer2Option;
        private CommandOption _deletedOption;
        private CommandOption _unlistedOption;
        private CommandOption _hasIconOption;
        private CommandOption _noGallery;
        private CommandOption _database;

        public CheckPackageCommand(
            PackageService packageService,
            PackageConsistencyService consistencyService,
            PackageConsistencyContextBuilder contextBuilder,
            ILogger<CheckPackageCommand> logger)
        {
            _packageService = packageService;
            _consistencyService = consistencyService;
            _contextBuilder = contextBuilder;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
            _idArgument = app.Argument(
                "package ID",
                "The package ID to check.",
                x => x.IsRequired());
            _versionArgument = app.Argument(
                "package version",
                "The package version to check.",
                x => x.IsRequired());

            _semVer2Option = app.Option(
                "--semver2",
                "Consider the package to be SemVer 2.0.0.",
                CommandOptionType.NoValue);
            _deletedOption = app.Option(
                "--deleted",
                "Consider the package to be deleted.",
                CommandOptionType.NoValue);
            _unlistedOption = app.Option(
                "--unlisted",
                "Consider the package to be unlisted.",
                CommandOptionType.NoValue);
            _hasIconOption = app.Option(
                "--has-icon",
                "Consider the package to have an icon.",
                CommandOptionType.NoValue);
            _noGallery = app.Option(
                "--no-gallery",
                "Don't use details from the gallery as a baseline. Instead, use explicit command-line options.",
                CommandOptionType.NoValue);
            _database = app.Option(
                "--database",
                "Use details from the ExplorePackages database as a baseline.",
                CommandOptionType.NoValue);
        }

        private string Id => _idArgument?.Value;
        private string Version => _versionArgument?.Value;
        private bool SemVer2 => _semVer2Option?.HasValue() ?? false;
        private bool Deleted => _deletedOption?.HasValue() ?? false;
        private bool Unlisted => _unlistedOption?.HasValue() ?? false;
        private bool HasIcon => _hasIconOption?.HasValue() ?? false;
        private bool NoGallery => _noGallery?.HasValue() ?? false;
        private bool Database => _database?.HasValue() ?? false;

        public async Task ExecuteAsync(CancellationToken token)
        {
            if (!NuGetVersion.TryParse(Version, out var parsedVersion))
            {
                _logger.LogError("The version '{Version}' could not be parsed.", Version);
                return;
            }

            var isSemVer2 = parsedVersion.IsSemVer2 || SemVer2;

            var state = new PackageConsistencyState();
            PackageConsistencyContext context;
            if (!NoGallery)
            {
                context = await _contextBuilder.CreateFromServerAsync(Id, Version, state);
            }
            else if (Database)
            {
                var package = await _packageService.GetPackageOrNullAsync(Id, Version);
                if (package == null)
                {
                    _logger.LogError("The package {Id} {Version} could not be found in the database.", Id, Version);
                    return;
                }
                context = package.ToConsistencyContext();
            }
            else if (Deleted)
            {
                context = _contextBuilder.CreateDeleted(Id, Version);
            }
            else
            {
                context = _contextBuilder.CreateAvailable(Id, Version, isSemVer2, !Unlisted, HasIcon);
            }

            var report = await _consistencyService.GetReportAsync(context, state, NullProgressReporter.Instance);
            var reportJson = JsonConvert.SerializeObject(
                report,
                new JsonSerializerSettings
                {
                    Converters =
                    {
                        new NuspecJsonConverter(),
                        new StringEnumConverter(),
                    },
                    Formatting = Formatting.Indented,
                });
            _logger.LogInformation(reportJson);
        }

        private class NuspecJsonConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(XDocument);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
            }
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => Database;
        public bool IsSingleton() => false;
    }
}
