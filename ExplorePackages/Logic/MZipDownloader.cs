using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using Knapcode.MiniZip;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipDownloader
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly MZipFormat _mZipFormat;

        public MZipDownloader(
            PackagePathProvider pathProvider,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpZipProvider httpZipProvider,
            MZipFormat mZipFormat)
        {
            _pathProvider = pathProvider;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _httpZipProvider = httpZipProvider;
            _mZipFormat = mZipFormat;
        }

        public async Task StoreMZipAsync(string id, string version, CancellationToken token)
        {
            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var contentUrl = _flatContainerClient.GetPackageContentUrl(baseUrl, id, version);
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();
            var latestPath = _pathProvider.GetLatestMZipPath(id, version);

            if (File.Exists(latestPath))
            {
                return;
            }

            using (var reader = await _httpZipProvider.GetReaderAsync(new Uri(contentUrl)))
            {
                await SafeFileWriter.WriteAsync(
                    latestPath,
                    destStream => _mZipFormat.WriteAsync(reader.Stream, destStream));
            }
        }
    }
}
