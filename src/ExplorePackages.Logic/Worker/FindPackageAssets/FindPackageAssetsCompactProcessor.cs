using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class FindPackageAssetsCompactProcessor : IMessageProcessor<FindPackageAssetsCompactMessage>
    {
        private readonly FindPackageAssetsStorageService _storageService;

        public FindPackageAssetsCompactProcessor(FindPackageAssetsStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task ProcessAsync(FindPackageAssetsCompactMessage message)
        {
            await _storageService.CompactAsync(message.Bucket);
        }
    }
}
