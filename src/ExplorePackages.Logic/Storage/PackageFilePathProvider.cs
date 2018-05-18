using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageFilePathProvider
    {
        private readonly ExplorePackagesSettings _settings;
        private readonly string _packagePath;
        private readonly PackageFilePathStyle _style;

        public PackageFilePathProvider(ExplorePackagesSettings settings, PackageFilePathStyle style)
        {
            _settings = settings;
            _packagePath = _settings.PackagePath.TrimEnd('/', '\\');
            _style = style;
        }

        public string GetLatestFilePath(string id, string version, FileArtifactType type)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();

            var fileName = GetFileName(lowerId, lowerVersion, type);

            var packageSpecificPath = GetPackageSpecificDirectory(lowerId, lowerVersion);

            return Path.Combine(packageSpecificPath, fileName);
        }

        private string GetFileName(string lowerId, string lowerVersion, FileArtifactType type)
        {
            var extension = GetExtension(type);

            string fileName;
            switch (_style)
            {
                case PackageFilePathStyle.TwoByteIdentityHash:
                    fileName = $"latest.{extension}";
                    break;
                default:
                    switch (type)
                    {
                        case FileArtifactType.Nuspec:
                            fileName = $"{lowerId}.{extension}";
                            break;
                        default:
                            fileName = $"{lowerId}.{lowerVersion}.{extension}";
                            break;
                    }
                    break;
            }

            return fileName;
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

        private string GetPackageSpecificDirectory(string lowerId, string lowerVersion)
        {
            string[] pieces;
            switch (_style)
            {
                case PackageFilePathStyle.IdVersion:
                    pieces = new[]
                    {
                        _packagePath,
                        lowerId,
                        lowerVersion,
                    };
                    break;
                case PackageFilePathStyle.FourIdLetters:
                    pieces = new[]
                    {
                        _packagePath,
                        GenerateLetterSubdirectories(lowerId, levels: 4),
                        lowerId,
                        lowerVersion
                    };
                    break;
                case PackageFilePathStyle.TwoByteIdentityHash:
                    pieces = new[]
                    {
                        _packagePath,
                        GenerateIdentityHashSubdirectories($"{lowerId}/{lowerVersion}", bytes: 2),
                        lowerId,
                        lowerVersion
                    };
                    break;
                default:
                    throw new NotSupportedException($"The package file path style {_style} is not supported.");
            }

            return Path.Combine(pieces);
        }

        private static string GenerateIdentityHashSubdirectories(string value, int bytes)
        {
            byte[] hash;
            using (var md5 = MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
            }

            var builder = new StringBuilder();
            for (var i = 0; i < bytes && i < hash.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(Path.DirectorySeparatorChar);
                }

                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }

        private static string GenerateLetterSubdirectories(string value, int levels)
        {
            const char ReplacementCharacter = '_';

            var builder = new StringBuilder();
            for (var i = 0; i < levels; i++)
            {
                if (i > 0)
                {
                    builder.Append(Path.DirectorySeparatorChar);
                }

                char piece;
                if (i >= value.Length)
                {
                    piece = ReplacementCharacter;
                }
                else
                {
                    piece = value[i];
                    if (!char.IsLetterOrDigit(piece) && piece != '_' && piece != '-')
                    {
                        piece = ReplacementCharacter;
                    }
                }

                builder.Append(piece);
            }

            return builder.ToString();
        }
    }
}
