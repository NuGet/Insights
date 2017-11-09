using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public interface INuspecQuery
    {
        string Name { get; }
        string CursorName { get; }
        bool IsMatch(XDocument nuspec);
    }
}
