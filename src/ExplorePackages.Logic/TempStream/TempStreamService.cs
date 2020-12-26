using System;
using System.IO;
using System.Security.Cryptography;
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

        public async Task<Stream> CopyToTempStreamAsync(Func<Stream> getStream, long length)
        {
            var result = await CopyToTempStreamAsync(getStream, length, () => null);
            return result.Stream;
        }

        public async Task<TempStreamResult> CopyToTempStreamAsync(Func<Stream> getStream, long length, Func<HashAlgorithm> getHashAlgorithm)
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

        public TempStreamWriter GetWriter() => _serviceProvider.GetRequiredService<TempStreamWriter>();
    }
}
