using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace NuGet.Insights
{
    public static class NuGetInsightsMessagePack
    {
        public static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions
            .Standard
            .WithResolver(CompositeResolver.Create(
                new IMessagePackFormatter[]
                {
                },
                new IFormatterResolver[]
                {
                    CsvRecordFormatterResolver.Instance,
                    StandardResolver.Instance
                }))
            .WithCompression(MessagePackCompression.Lz4Block);
    }
}
