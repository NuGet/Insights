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

        public string GetLatestNuspecFilePath(string id, string version)
        {
            string fileName;
            switch (_style)
            {
                case PackageFilePathStyle.TwoByteIdentityHash:
                    fileName = "latest.nuspec";
                    break;
                default:
                    fileName = $"{id.ToLowerInvariant()}.nuspec";
                    break;
            }

            var packageSpecificPath = GetPackageSpecificDirectory(id, version);
            return Path.Combine(packageSpecificPath, fileName);
        }

        public string GetLatestMZipFilePath(string id, string version)
        {
            string fileName;
            switch (_style)
            {
                case PackageFilePathStyle.TwoByteIdentityHash:
                    fileName = "latest.mzip";
                    break;
                default:
                    fileName = $"{id.ToLowerInvariant()}.{version.ToLowerInvariant()}.mzip";
                    break;
            }

            var packageSpecificPath = GetPackageSpecificDirectory(id, version);
            return Path.Combine(packageSpecificPath, fileName);
        }

        public string GetPackageSpecificDirectory(string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();

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
