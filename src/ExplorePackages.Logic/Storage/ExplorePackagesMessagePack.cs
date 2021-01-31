using MessagePack;
using MessagePack.Resolvers;

namespace Knapcode.ExplorePackages
{
    public static class ExplorePackagesMessagePack
    {
        public static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions
            .Standard
            .WithResolver(CompositeResolver.Create(
                CsvRecordFormatterResolver.Instance,
                StandardResolver.Instance))
            .WithCompression(MessagePackCompression.Lz4Block);
    }
}
