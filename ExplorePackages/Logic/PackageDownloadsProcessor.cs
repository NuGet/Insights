using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDownloadsProcessor
    {
        private readonly PackageDownloadsClient _client;

        public PackageDownloadsProcessor(PackageDownloadsClient client)
        {
            _client = client;
        }
    }
}
