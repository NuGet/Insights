using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public interface INuspecQuery
    {
        string Name { get; }
        string CursorName { get; }
        bool IsMatch(XDocument nuspec);
    }
}
