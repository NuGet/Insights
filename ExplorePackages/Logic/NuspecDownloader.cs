using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecDownloader
    {
        private const int BufferSize = 8192;
        
        private readonly PackagePathProvider _pathProvider;
        private readonly HttpSource _httpSource;
        private readonly ILogger _log;

        public NuspecDownloader(PackagePathProvider pathProvider, HttpSource httpSource, ILogger log)
        {
            _pathProvider = pathProvider;
            _httpSource = httpSource;
            _log = log;
        }

        public async Task<bool> StoreNuspecAsync(string id, string version, string url, CancellationToken token)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();

            using (var tempStream = new FileStream(
                Path.GetTempFileName(),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                BufferSize,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous))
            {
                var success = await _httpSource.ProcessStreamAsync(
                    new HttpSourceRequest(url, _log)
                    {
                        IgnoreNotFounds = true,
                    },
                    async networkStream =>
                    {
                        if (networkStream == null)
                        {
                            return false;
                        }

                        await networkStream.CopyToAsync(tempStream);
                        return true;
                    },
                    _log,
                    token);

                if (!success)
                {
                    return false;
                }

                var hash = await HashStreamAsync(tempStream);
                var lockPath = _pathProvider.GetPackageSpecificPath(id, version);
                var uniquePath = _pathProvider.GetUniqueNuspecPath(id, version, hash);
                var latestPath = _pathProvider.GetLatestNuspecPath(id, version);

                return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                    lockPath,
                    async innerToken =>
                    {
                        if (File.Exists(uniquePath))
                        {
                            return true;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(uniquePath));

                        tempStream.Position = 0;
                        using (var fileStream = new FileStream(
                            uniquePath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            BufferSize,
                            FileOptions.Asynchronous))
                        {
                            await tempStream.CopyToAsync(fileStream);
                        }

                        tempStream.Position = 0;
                        using (var fileStream = new FileStream(
                            latestPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            BufferSize,
                            FileOptions.Asynchronous))
                        {
                            await tempStream.CopyToAsync(fileStream);
                        }

                        return true;
                    },
                    token);
            }
        }

        private async Task<string> HashStreamAsync(Stream stream)
        {
            stream.Position = 0;

            using (var sha256 = SHA256.Create())
            {
                byte[] buffer = new byte[BufferSize];
                int read;

                while ((read = await stream.ReadAsync(buffer, 0, BufferSize)) > 0)
                {
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                }

                sha256.TransformFinalBlock(new byte[0], 0, 0);

                return BitConverter
                    .ToString(sha256.Hash)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
