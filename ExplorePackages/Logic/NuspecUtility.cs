using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public static class NuspecUtility
    {
        public static XDocument LoadXml(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };

            using (var streamReader = new StreamReader(stream))
            using (var xmlReader = XmlReader.Create(streamReader, settings))
            {
                var document = XDocument.Load(xmlReader, LoadOptions.None);

                // Make the document immutable.
                document.Changing += (s, ev) =>
                {
                    throw new NotSupportedException();
                };

                return document;
            }
        }

        public static XElement GetRepository(XDocument nuspec)
        {
            var metadataEl = GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return null;
            }

            var ns = metadataEl.GetDefaultNamespace();
            return metadataEl.Element(ns.GetName("repository"));
        }

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
