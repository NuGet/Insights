using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<NuspecDownloader> _logger;

        public NuspecDownloader(
            PackagePathProvider pathProvider,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpSource httpSource,
            ILogger<NuspecDownloader> logger)
        {
            _pathProvider = pathProvider;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _httpSource = httpSource;
            _logger = logger;
        }

        public async Task<bool> StoreNuspecAsync(string id, string version, CancellationToken token)
        {
            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var manifestUrl = _flatContainerClient.GetPackageManifestUrl(baseUrl, id, version);
            return await StoreNuspecAsync(id, version, manifestUrl, token);
        }

        private async Task<bool> StoreNuspecAsync(string id, string version, string url, CancellationToken token)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();
            var latestPath = _pathProvider.GetLatestNuspecPath(id, version);

            if (File.Exists(latestPath))
            {
                return true;
            }

            using (var tempStream = new FileStream(
                Path.GetTempFileName(),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                BufferSize,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous))
            {
                var nuGetLogger = _logger.ToNuGetLogger();
                var success = await _httpSource.ProcessStreamAsync(
                    new HttpSourceRequest(url, nuGetLogger)
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
                    nuGetLogger,
                    token);

                if (!success)
                {
                    return false;
                }

                var hash = await HashStreamAsync(tempStream);
                var lockPath = _pathProvider.GetPackageSpecificPath(id, version);
                var uniquePath = _pathProvider.GetUniqueNuspecPath(id, version, hash);

                return await NuGet.Common.ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                    lockPath,
                    async innerToken =>
                    {
                        if (File.Exists(uniquePath) && File.Exists(latestPath))
                        {
                            return true;
                        }

                        tempStream.Position = 0;
                        await SafeFileWriter.WriteAsync(uniquePath, tempStream);

                        tempStream.Position = 0;
                        await SafeFileWriter.WriteAsync(latestPath, tempStream);

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
