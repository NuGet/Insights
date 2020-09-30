using Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets;
using Knapcode.ExplorePackages.Logic.Worker.RunRealRestore;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class SchemaSerializer
    {
        private static readonly GenericSchemaSerializer Serializer = new GenericSchemaSerializer(new ISchema[]
        {
            new SchemaV1<BulkEnqueueMessage>("be"),
            new SchemaV1<CatalogIndexScanMessage>("cis"),
            new SchemaV1<CatalogPageScanMessage>("cps"),
            new SchemaV1<CatalogLeafScanMessage>("cls"),
            new SchemaV1<FindPackageAssetsCompactMessage>("fpa.c"),
            new SchemaV1<RunRealRestoreMessage>("rrr"),
            new SchemaV1<RunRealRestoreCompactMessage>("rrr.c"),

            new SchemaV1<FindLatestLeavesParameters>("fll"),
            new SchemaV1<FindPackageAssetsParameters>("fpa"),
        });

        private readonly ILogger<SchemaSerializer> _logger;

        public SchemaSerializer(ILogger<SchemaSerializer> logger)
        {
            _logger = logger;
        }

        public ISerializedEntity Serialize(BulkEnqueueMessage message) => Serializer.Serialize(message);
        public ISerializedEntity Serialize(CatalogIndexScanMessage message) => Serializer.Serialize(message);
        public ISerializedEntity Serialize(CatalogPageScanMessage message) => Serializer.Serialize(message);
        public ISerializedEntity Serialize(CatalogLeafScanMessage message) => Serializer.Serialize(message);
        public ISerializedEntity Serialize(FindPackageAssetsCompactMessage message) => Serializer.Serialize(message);
        public ISerializedEntity Serialize(RunRealRestoreMessage message) => Serializer.Serialize(message);
        public ISerializedEntity Serialize(RunRealRestoreCompactMessage message) => Serializer.Serialize(message);

        public ISerializedEntity Serialize(FindLatestLeavesParameters parameters) => Serializer.Serialize(parameters);
        public ISerializedEntity Serialize(FindPackageAssetsParameters parameters) => Serializer.Serialize(parameters);

        public object Deserialize(string message) => Serializer.Deserialize(message, _logger);
    }
}
