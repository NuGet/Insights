using MessagePack;
using MessagePack.Formatters;

namespace Knapcode.ExplorePackages.VersionSets
{
    public class VersionSetDictionaryFormatter : DictionaryFormatterBase<string, CaseInsensitiveDictionary<bool>, VersionSetDictionary>
    {
        public static VersionSetDictionaryFormatter Instance { get; } = new VersionSetDictionaryFormatter();

        protected override VersionSetDictionary Create(int count, MessagePackSerializerOptions options)
        {
            return new VersionSetDictionary();
        }

        protected override void Add(VersionSetDictionary collection, int index, string key, CaseInsensitiveDictionary<bool> value, MessagePackSerializerOptions options)
        {
            collection.Add(key, value);
        }
    }
}
