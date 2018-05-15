using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecStore
    {
        private const int BufferSize = 8192;
        
        private readonly PackagePathProvider _pathProvider;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpSource _httpSource;
        private readonly ILogger<NuspecStore> _logger;

        public NuspecStore(
            PackagePathProvider pathProvider,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpSource httpSource,
            ILogger<NuspecStore> logger)
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
            var url = _flatContainerClient.GetPackageManifestUrl(baseUrl, id, version);

            var nuGetLogger = _logger.ToNuGetLogger();
            return await _httpSource.ProcessStreamAsync(
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

                    var latestPath = _pathProvider.GetLatestNuspecPath(id, version);
                    await SafeFileWriter.WriteAsync(
                        latestPath,
                        networkStream,
                        _logger);
                    return true;
                },
                nuGetLogger,
                token);
        }

        public NuspecContext GetNuspecContext(string id, string version)
        {
            var path = _pathProvider.GetLatestNuspecPath(id, version);

            Stream stream = null;
            try
            {
                stream = new FileStream(path, FileMode.Open);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return new NuspecContext(path, exists: false, document: null);
            }
            catch
            {
                stream?.Dispose();
                throw;
            }

            using (stream)
            {
                try
                {
                    var document = XmlUtility.LoadXml(stream);
                    return new NuspecContext(path, exists: true, document: document);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not parse .nuspec for {Id} {Version}: {Path}", id, version, path);
                    throw;
                }
            }
        }
    }
}
