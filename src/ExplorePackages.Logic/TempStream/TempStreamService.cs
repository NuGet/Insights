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

        public TempStreamWriter GetWriter() => _serviceProvider.GetRequiredService<TempStreamWriter>();
    }
}
