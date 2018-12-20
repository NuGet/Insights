using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageBlobNameProvider
    {
        public string GetLatestBlobName(string id, string version, FileArtifactType type)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();
            var extension = GetExtension(type);

            var blobName = $"{lowerId}/{lowerVersion}/latest.{extension}";

            return blobName;
        }

        public bool TryParseLatestBlobName(string original, ILogger logger, out ParsedPackageBlobName parsed)
        {
            parsed = null;

            var pieces = original.Split('/');
            if (pieces.Length != 3)
            {
                logger.LogWarning("Found blob with more than three '/' splitted pieces. Blob name: {Name}", original);
                return false;
            }

            var id = pieces[0];
            var version = pieces[1];

            var extension = Path.GetExtension(original);
            string old;
            string canonical;
            FileArtifactType type;
            if (extension == ".nuspec")
            {
                old = string.Join("/", id, version, $"{id}{extension}");
                type = FileArtifactType.Nuspec;
                canonical = GetLatestBlobName(id, version, type);
            }
            else if (extension == ".mzip")
            {
                old = string.Join("/", id, version, $"{id}.{version}{extension}");
                type = FileArtifactType.MZip;
                canonical = GetLatestBlobName(id, version, type);
            }
            else
            {
                logger.LogWarning("Found blob with unexpected extension. Blob name: {Name}", original);
                return false;
            }

            parsed = new ParsedPackageBlobName(original, canonical, id, version, type);

            if (original == canonical)
            {
                return true;
            }

            if (original != old)
            {
                logger.LogWarning(
                    "Found blob with unexpected file name. Expected file name: {Expected}. Blob name: {Name}",
                    old,
                    original);
                return false;
            }

            return true;
        }

        private static string GetExtension(FileArtifactType type)
        {
            string extension;
            switch (type)
            {
                case FileArtifactType.Nuspec:
                    extension = "nuspec";
                    break;
                case FileArtifactType.MZip:
                    extension = "mzip";
                    break;
                default:
                    throw new NotSupportedException($"The file artifact type {type} is not supported.");
            }

            return extension;
        }
    }

}
