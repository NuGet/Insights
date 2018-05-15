using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipStore
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly MZipFormat _mZipFormat;
        private readonly ILogger<MZipStore> _logger;

        public MZipStore(
            PackagePathProvider pathProvider,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpZipProvider httpZipProvider,
            MZipFormat mZipFormat,
            ILogger<MZipStore> logger)
        {
            _pathProvider = pathProvider;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _httpZipProvider = httpZipProvider;
            _mZipFormat = mZipFormat;
            _logger = logger;
        }

        public async Task StoreMZipAsync(string id, string version, CancellationToken token)
        {
            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var url = _flatContainerClient.GetPackageContentUrl(baseUrl, id, version);

            using (var reader = await _httpZipProvider.GetReaderAsync(new Uri(url)))
            {
                var path = _pathProvider.GetLatestMZipPath(id, version);

                await SafeFileWriter.WriteAsync(
                    path,
                    destStream => _mZipFormat.WriteAsync(reader.Stream, destStream),
                    _logger);
            }
        }

        public async Task<Stream> GetMZipStreamAsync(string id, string version, CancellationToken token)
        {
            var path = _pathProvider.GetLatestMZipPath(id, version);

            Stream stream = null;
            try
            {
                stream = new FileStream(path, FileMode.Open);
                return await _mZipFormat.ReadAsync(stream);
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
                stream?.Dispose();
                throw;
            }
        }
    }
}
