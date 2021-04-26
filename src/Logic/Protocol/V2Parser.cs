using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages
{
    /// <summary>
    /// Based off of:
    /// <see cref="https://github.com/NuGet/NuGet.Client/blob/572962f1b1ee890533e5a75163264bd021426dc7/src/NuGet.Core/NuGet.Protocol/LegacyFeed/V2FeedParser.cs"/>
    /// </summary>
    public class V2Parser
    {
        private const string W3Atom = "http://www.w3.org/2005/Atom";
        private const string MetadataNS = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
        private const string DataServicesNS = "http://schemas.microsoft.com/ado/2007/08/dataservices";

        private static readonly XName _xnameEntry = XName.Get("entry", W3Atom);
        private static readonly XName _xnameProperties = XName.Get("properties", MetadataNS);
        private static readonly XName _xnameId = XName.Get("Id", DataServicesNS);
        private static readonly XName _xnameVersion = XName.Get("Version", DataServicesNS);
        private static readonly XName _xnameCreated = XName.Get("Created", DataServicesNS);
        private static readonly XName _xnameLastEdited = XName.Get("LastEdited", DataServicesNS);
        private static readonly XName _xnameLastUpdated = XName.Get("LastUpdated", DataServicesNS);
        private static readonly XName _xnamePublished = XName.Get("Published", DataServicesNS);

        public IReadOnlyList<V2Package> ParsePage(XDocument doc)
        {
            if (doc.Root.Name == _xnameEntry)
            {
                return new[] { ParsePackage(doc.Root) };
            }
            else
            {
                return doc
                    .Root
                    .Elements(_xnameEntry)
                    .Select(x => ParsePackage(x))
                    .ToList();
            }
        }

        private V2Package ParsePackage(XElement element)
        {
            var properties = element.Element(_xnameProperties);

            var id = properties.Element(_xnameId).Value.Trim();
            var version = properties.Element(_xnameVersion).Value.Trim();
            var created = properties.Element(_xnameCreated).Value.Trim();
            var lastEdited = properties.Element(_xnameLastEdited).Value?.Trim();
            var lastUpdated = properties.Element(_xnameLastUpdated).Value.Trim();
            var published = properties.Element(_xnamePublished).Value.Trim();

            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            var parsedCreated = DateTimeOffset.Parse(
                created,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal);

            DateTimeOffset? parsedLastEdited;
            if (string.IsNullOrEmpty(lastEdited))
            {
                parsedLastEdited = null;
            }
            else
            {
                parsedLastEdited = DateTimeOffset.Parse(
                    lastEdited,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal);
            }

            var parsedLastUpdated = DateTimeOffset.Parse(
                lastUpdated,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal);

            var parsedPublished = DateTimeOffset.Parse(
                published,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal);

            return new V2Package
            {
                Id = id,
                Version = normalizedVersion,
                Created = parsedCreated,
                LastEdited = parsedLastEdited,
                LastUpdated = parsedLastUpdated,
                Published = parsedPublished,
            };
        }
    }
}
