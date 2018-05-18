using System;

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
