using System;

namespace Knapcode.ExplorePackages.Worker.OwnersToCsv
{
    public partial record PackageOwnerRecord : ICsvRecord<PackageOwnerRecord>
    {
        public DateTimeOffset AsOfTimestamp { get; set; }

        [KustoPartitionKey]
        public string LowerId { get; set; }

        public string Id { get; set; }
        public string Owners { get; set; }
    }
}
