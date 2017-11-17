using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.Versioning;

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

        public static string GetOriginalVersion(XDocument nuspec)
        {
            var metadataEl = GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return null;
            }

            var ns = metadataEl.GetDefaultNamespace();
            var versionEl = metadataEl.Element(ns.GetName("version"));
            if (versionEl == null)
            {
                return null;
            }

            return versionEl.Value.Trim();
        }

        public static bool IsSemVer2(XDocument nuspec)
        {
            return HasSemVer2PackageVersion(nuspec)
                || HasSemVer2DependencyVersion(nuspec);
        }

        public static bool HasSemVer2DependencyVersion(XDocument nuspec)
        {
            var metadataEl = GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return false;
            }

            var ns = metadataEl.GetDefaultNamespace();
            var dependencyEls = GetDependencies(nuspec);
            foreach (var dependencyEl in dependencyEls)
            {
                var dependencyVersion = dependencyEl.Attribute("version")?.Value;
                if (!string.IsNullOrEmpty(dependencyVersion)
                    && VersionRange.TryParse(dependencyVersion, out var parsed))
                {
                    if (parsed.HasLowerBound && parsed.MinVersion.IsSemVer2)
                    {
                        return true;
                    }

                    if (parsed.HasUpperBound && parsed.MaxVersion.IsSemVer2)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasSemVer2PackageVersion(XDocument nuspec)
        {
            var metadataEl = GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return false;
            }

            var ns = metadataEl.GetDefaultNamespace();
            var version = metadataEl.Element(ns.GetName("version"));
            if (version != null)
            {
                if (NuGetVersion.TryParse(version.Value, out var parsed)
                    && parsed.IsSemVer2)
                {
                    return true;
                }
            }

            return false;
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
