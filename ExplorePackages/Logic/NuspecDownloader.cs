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
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpSource _httpSource;
        private readonly ILogger _log;

        public NuspecDownloader(
            PackagePathProvider pathProvider,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpSource httpSource,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _httpSource = httpSource;
            _log = log;
        }

        public async Task<bool> StoreNuspecAsync(string id, string version, CancellationToken token)
        {
            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var manifestUrl = _flatContainerClient.GetPackageManifestUrl(baseUrl, id, version);
            return await StoreNuspecAsync(id, version, manifestUrl, token);
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
                        if (File.Exists(uniquePath) && File.Exists(latestPath))
                        {
                            return true;
                        }

                        tempStream.Position = 0;
                        await SafelyWriteFileAsync(uniquePath, tempStream);

                        tempStream.Position = 0;
                        await SafelyWriteFileAsync(latestPath, tempStream);

                        return true;
                    },
                    token);
            }
        }

        private static async Task SafelyWriteFileAsync(string path, Stream sourceStream)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var newPath = $"{path}.new";
            var oldPath = $"{path}.old";

            using (var destStream = new FileStream(
                newPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous))
            {
                await sourceStream.CopyToAsync(destStream);
            }
            
            try
            {
                File.Replace(newPath, path, oldPath);
                File.Delete(oldPath);
            }
            catch (FileNotFoundException)
            {
                File.Move(newPath, path);
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
