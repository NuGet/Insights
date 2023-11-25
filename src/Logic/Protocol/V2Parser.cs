// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml.Linq;

#nullable enable

namespace NuGet.Insights
{
    /// <summary>
    /// Based off of:
    /// https://github.com/NuGet/NuGet.Client/blob/572962f1b1ee890533e5a75163264bd021426dc7/src/NuGet.Core/NuGet.Protocol/LegacyFeed/V2FeedParser.cs
    /// </summary>
    public class V2Parser
    {
        private const string W3Atom = "http://www.w3.org/2005/Atom";
        private const string MetadataNS = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
        private const string DataServicesNS = "http://schemas.microsoft.com/ado/2007/08/dataservices";

        private static readonly XName XnameEntry = XName.Get("entry", W3Atom);
        private static readonly XName XnameProperties = XName.Get("properties", MetadataNS);
        private static readonly XName XnameId = XName.Get("Id", DataServicesNS);
        private static readonly XName XnameVersion = XName.Get("Version", DataServicesNS);
        private static readonly XName XnameCreated = XName.Get("Created", DataServicesNS);
        private static readonly XName XnameLastEdited = XName.Get("LastEdited", DataServicesNS);
        private static readonly XName XnameLastUpdated = XName.Get("LastUpdated", DataServicesNS);
        private static readonly XName XnamePublished = XName.Get("Published", DataServicesNS);

        public IReadOnlyList<V2Package> ParsePage(XDocument doc)
        {
            if (doc.Root is null)
            {
                throw new ArgumentException("The provided XML document must have a root element.");
            }

            if (doc.Root.Name == XnameEntry)
            {
                return new[] { ParsePackage(doc.Root) };
            }
            else
            {
                return doc
                    .Root
                    .Elements(XnameEntry)
                    .Select(x => ParsePackage(x))
                    .ToList();
            }
        }

        private V2Package ParsePackage(XElement element)
        {
            var properties = element.Element(XnameProperties)!;

            var id = properties.Element(XnameId)!.Value.Trim();
            var version = properties.Element(XnameVersion)!.Value.Trim();
            var created = properties.Element(XnameCreated)!.Value.Trim();
            var lastEdited = properties.Element(XnameLastEdited)!.Value.Trim();
            var lastUpdated = properties.Element(XnameLastUpdated)!.Value.Trim();
            var published = properties.Element(XnamePublished)!.Value.Trim();

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
