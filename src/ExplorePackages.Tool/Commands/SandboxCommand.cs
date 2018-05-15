using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class SandboxCommand : ICommand
    {
        private readonly ExplorePackagesSettings _settings;

        public SandboxCommand(
            ExplorePackagesSettings settings)
        {
            _settings = settings;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            foreach (var idDir in Directory.EnumerateDirectories(_settings.PackagePath, "*", SearchOption.TopDirectoryOnly))
            {
                foreach (var versionDir in Directory.EnumerateDirectories(idDir, "*", SearchOption.TopDirectoryOnly))
                {
                    var nuspecsDir = Path.Combine(versionDir, "nuspecs");
                    Console.Write(nuspecsDir + "...");
                    if (Directory.Exists(nuspecsDir))
                    {
                        Directory.Delete(nuspecsDir, recursive: true);
                        Console.WriteLine(" deleted.");
                    }
                    else
                    {
                        Console.WriteLine(" does not exist.");
                    }
                }
            }
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
