using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Knapcode.ExplorePackages
{
    public class TempStreamService
    {
        private readonly IServiceProvider _serviceProvider;

        public TempStreamService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<TempStreamResult> CopyToTempStreamAsync(Func<Stream> getStream, long length, Func<IIncrementalHash> getHashAlgorithm)
        {
            var writer = GetWriter();
            TempStreamResult result;
            do
            {
                using var src = getStream();
                using var hashAlgorithm = getHashAlgorithm();
                result = await writer.CopyToTempStreamAsync(src, length, hashAlgorithm);
            }
            while (result.Type == TempStreamResultType.NeedNewStream);
            return result;
        }

        public TempStreamWriter GetWriter()
        {
            return _serviceProvider.GetRequiredService<TempStreamWriter>();
        }
    }
}
