using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Knapcode.ExplorePackages.Logic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Common;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Commands
{
    public class CheckPackageCommand : ICommand
    {
        private readonly PackageConsistencyService _service;
        private readonly PackageQueryContextBuilder _contextBuilder;
        private readonly ILogger _log;

        public CheckPackageCommand(PackageConsistencyService service, PackageQueryContextBuilder contextBuilder, ILogger log)
        {
            _service = service;
            _contextBuilder = contextBuilder;
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var argList = args.ToList();
            var hasSemVer2Arg = HasSemVer2Arg(argList);
            var hasDeletedArg = HasDeletedArg(argList);
            var hasGalleryArg = HasGalleryArg(argList);
            var hasDatabaseArg = HasDatabaseArg(argList);

            if (argList.Count < 3)
            {
                _log.LogError("The second and third parameters should be the package ID then the package version.");
                return;
            }

            var id = argList[1].Trim();
            var version = argList[2].Trim();

            if (!NuGetVersion.TryParse(version, out var parsedVersion))
            {
                _log.LogError($"The version '{version}' could not be parsed.");
                return;
            }

            var isSemVer2 = parsedVersion.IsSemVer2 || hasSemVer2Arg;

            var state = new PackageConsistencyState();
            PackageQueryContext context;
            if (hasGalleryArg)
            {
                context = await _contextBuilder.GetPackageQueryContextFromGalleryAsync(id, version, state);
            }
            else if (hasDatabaseArg)
            {
                context = await _contextBuilder.GetPackageQueryContextFromDatabaseAsync(id, version);
                if (context == null)
                {
                    _log.LogError($"The package {id} {version} could not be found in the database.");
                    return;
                }
            }
            else if (hasDeletedArg)
            {
                context = _contextBuilder.CreateDeletedPackageQueryContext(id, version);
            }
            else
            {
                context = _contextBuilder.CreateAvailablePackageQueryContext(id, version, isSemVer2);
            }

            var report = await _service.GetReportAsync(context, state);
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
            Console.WriteLine(reportJson);
        }

        private bool HasGalleryArg(List<string> argList)
        {
            return HasArg(argList, "-gallery");
        }

        private bool HasDatabaseArg(List<string> argList)
        {
            return HasArg(argList, "-database");
        }

        private bool HasDeletedArg(List<string> argList)
        {
            return HasArg(argList, "-deleted");
        }

        private bool HasSemVer2Arg(List<string> argList)
        {
            return HasArg(argList, "-semver2");
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return HasDatabaseArg(args.ToList());
        }

        private bool HasArg(List<string> args, string arg)
        {
            var hasArg = false;
            for (var i = 0; i < args.Count; i++)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(args[i], arg))
                {
                    hasArg = true;
                    args.RemoveAt(i);
                    i--;
                }
            }

            return hasArg;
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
    }
}
