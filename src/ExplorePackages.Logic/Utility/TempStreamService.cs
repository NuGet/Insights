using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class TempStreamService
    {
        private readonly IOptions<ExplorePackagesSettings> _options;
        private readonly ILogger<TempStreamService> _logger;

        public TempStreamService(
            IOptions<ExplorePackagesSettings> options,
            ILogger<TempStreamService> logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task<Stream> CopyToTempStreamAsync(Func<Stream> getStream, long length)
        {
            var writer = GetWriter();
            TempStreamResult result;
            do
            {
                using var src = getStream();
                result = await writer.CopyToTempStreamAsync(src, length);
            }
            while (!result.Success);
            return result.Stream;
        }

        public TempStreamWriter GetWriter()
        {
            return new TempStreamWriter(
                _options.Value.MaxInMemoryTempStreamSize,
                _options.Value.TempDirectories,
                _logger);
        }
    }
}
