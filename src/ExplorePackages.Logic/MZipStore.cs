using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.MiniZip;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipStore
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly MZipFormat _mZipFormat;

        public MZipStore(
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
            var latestPath = _pathProvider.GetLatestMZipPath(id, version);

            if (File.Exists(latestPath))
            {
                return;
            }

            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var contentUrl = _flatContainerClient.GetPackageContentUrl(baseUrl, id, version);

            using (var reader = await _httpZipProvider.GetReaderAsync(new Uri(contentUrl)))
            {
                await SafeFileWriter.WriteAsync(
                    latestPath,
                    destStream => _mZipFormat.WriteAsync(reader.Stream, destStream));
            }
        }

        public async Task<Stream> GetMZipStreamAsync(string id, string version, CancellationToken token)
        {
            var latestPath = _pathProvider.GetLatestMZipPath(id, version);

            Stream fileStream = null;
            try
            {
                fileStream = new FileStream(latestPath, FileMode.Open);
                return await _mZipFormat.ReadAsync(fileStream);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch
            {
                fileStream?.Dispose();
                throw;
            }
        }
    }
}
