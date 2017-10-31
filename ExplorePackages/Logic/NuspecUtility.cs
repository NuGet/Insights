using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public static class NuspecUtility
    {
        public static XElement GetMetadata(XDocument nuspec)
        {
            if (nuspec == null)
            {
                return null;
            }

            return nuspec
                .Root
                .Elements()
                .Where(x => x.Name.LocalName == "metadata")
                .FirstOrDefault();
        }

        public static IEnumerable<XElement> GetDependencies(XDocument nuspec)
        {
            var metadataEl = GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return Enumerable.Empty<XElement>();
            }

            var ns = metadataEl.GetDefaultNamespace();

            var dependenciesEl = metadataEl.Element(ns.GetName("dependencies"));
            if (dependenciesEl == null)
            {
                return Enumerable.Empty<XElement>();
            }

            var dependenyName = ns.GetName("dependency");

            return dependenciesEl
                .Elements(ns.GetName("group"))
                .SelectMany(x => x.Elements(dependenyName))
                .Concat(dependenciesEl.Elements(dependenyName));
        }
    }
}
