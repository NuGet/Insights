using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class V2ToDatabaseCommand : ICommand
    {
        private readonly V2ToDatabaseProcessor _processor;
        private readonly ILogger<V2ToDatabaseCommand> _logger;
        private CommandOption _idsOption;
        private CommandOption _versionsOption;

        public V2ToDatabaseCommand(V2ToDatabaseProcessor processor, ILogger<V2ToDatabaseCommand> logger)
        {
            _processor = processor;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
            const string idTemplate = "--id";
            const string versionTemplate = "--version";

            _idsOption = app.Option(
                idTemplate,
                $"The IDs of the specific packages to process. This must have the same number of values specified as the {versionTemplate} option.",
                CommandOptionType.MultipleValue);
            _versionsOption = app.Option(
                "--version",
                $"The versions of the specific package to process. This must have the same number of values specified as the {idTemplate} option.",
                CommandOptionType.MultipleValue);
        }

        private IReadOnlyList<string> Ids => _idsOption?.Values ?? new List<string>();
        private IReadOnlyList<string> Versions => _versionsOption?.Values ?? new List<string>();

        public async Task ExecuteAsync(CancellationToken token)
        {
            if (Ids.Any() || Versions.Any())
            {
                if (Ids.Count != Versions.Count)
                {
                    _logger.LogError(
                        $"There are {{IdsCount}} {_idsOption.Template} values specified but {{VersionsCount}} " +
                        $"{_versionsOption.Template} values specified. There must be the same number.",
                        Ids.Count,
                        Versions.Count);
                    return;
                }

                var identities = Ids
                    .Zip(Versions, (id, version) => new PackageIdentity(id.Trim(), NuGetVersion.Parse(version).ToNormalizedString()))
                    .ToList();

                await _processor.UpdateAsync(identities);
            }
            else
            {
                await _processor.UpdateAsync(V2OrderByTimestamp.Created);

                await _processor.UpdateAsync(V2OrderByTimestamp.LastEdited);
            }

        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsSingleton() => true;
    }
}
