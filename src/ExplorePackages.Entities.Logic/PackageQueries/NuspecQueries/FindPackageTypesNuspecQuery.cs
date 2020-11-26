using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindPackageTypesNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindPackageTypesNuspecQuery;
        public string CursorName => CursorNames.FindPackageTypesNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var metadataEl = NuspecUtility.GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return false;
            }

            var ns = metadataEl.GetDefaultNamespace();
            var packageTypesEl = metadataEl.Element(ns.GetName("packageTypes"));
            return packageTypesEl != null;
        }
    }
}
