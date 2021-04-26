using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages
{
    public static class NuspecUtility
    {
        private static readonly Regex IsAlphabet = new Regex(@"^[a-zA-Z]+$", RegexOptions.Compiled);
        private static readonly NuGetFrameworkNameComparer NuGetFrameworkNameComparer = new NuGetFrameworkNameComparer();
        private static readonly HashSet<string> CollidingMetadataElementNames = new HashSet<string>
        {
            "created",
            "dependencyGroups",
            "isPrerelease",
            "lastEdited",
            "listed",
            "packageEntries",
            "packageHash",
            "packageHashAlgorithm",
            "packageSize",
            "published",
            "supportedFrameworks",
            "verbatimVersion",
        };
        private static readonly HashSet<string> BooleanMetadataElementNames = new HashSet<string>
        {
            "developmentDependency",
            "requireLicenseAcceptance",
            "serviceable",
        };


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

        public static string GetOriginalId(XDocument nuspec)
        {
            var metadataEl = GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return null;
            }

            var ns = metadataEl.GetDefaultNamespace();
            var idEl = metadataEl.Element(ns.GetName("id"));
            if (idEl == null)
            {
                return null;
            }

            return idEl.Value.Trim();
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
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (dependency.ParsedVersionRange != null)
                {
                    if (dependency.ParsedVersionRange.HasLowerBound
                        && dependency.ParsedVersionRange.MinVersion.IsSemVer2)
                    {
                        return true;
                    }

                    if (dependency.ParsedVersionRange.HasUpperBound
                        && dependency.ParsedVersionRange.MaxVersion.IsSemVer2)
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

        public static XmlDependencyGroups GetXmlDependencyGroups(XDocument nuspec)
        {
            var metadataEl = GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return XmlDependencyGroups.Empty;
            }

            var ns = metadataEl.GetDefaultNamespace();

            var dependenciesEl = metadataEl.Element(ns.GetName("dependencies"));
            if (dependenciesEl == null)
            {
                return XmlDependencyGroups.Empty;
            }

            var dependencyName = ns.GetName("dependency");
            var groupName = ns.GetName("group");

            var legacyDependencies = dependenciesEl
                .Elements(dependencyName)
                .Select(GetXmlDependency)
                .ToList();

            var groups = new List<XmlDependencyGroup>();
            foreach (var groupEl in dependenciesEl.Elements(groupName))
            {
                var targetFramework = groupEl.Attribute("targetFramework")?.Value;
                var groupDependencies = groupEl
                    .Elements(dependencyName)
                    .Select(GetXmlDependency)
                    .ToList();
                groups.Add(new XmlDependencyGroup(
                    targetFramework,
                    groupDependencies));
            }

            return new XmlDependencyGroups(legacyDependencies, groups);
        }

        public static IReadOnlyList<Dependency> GetDependencies(XDocument nuspec)
        {
            var groups = GetParsedDependencyGroups(nuspec);

            return groups
                .Groups
                .SelectMany(x => x.Dependencies)
                .Concat(groups.Dependencies)
                .ToList();
        }

        public static IEnumerable<string> GetInvalidDependencyIds(XDocument nuspec)
        {
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (!StrictPackageIdValidator.IsValid(dependency.Id))
                {
                    yield return dependency.Id;
                }
            }
        }

        public static bool HasMixedDependencyGroupStyles(XDocument nuspec)
        {
            var groups = GetParsedDependencyGroups(nuspec);

            return groups.Dependencies.Any() && groups.Groups.Any();
        }

        public static IEnumerable<string> GetUnsupportedDependencyTargetFrameworks(XDocument nuspec)
        {
            foreach (var group in GetParsedDependencyGroups(nuspec).Groups)
            {
                if (group.ParsedTargetFramework != null
                    && group.ParsedTargetFramework.IsUnsupported)
                {
                    yield return group.TargetFramework;
                }
            }
        }

        public static ILookup<NuGetFramework, string> GetDuplicateNormalizedDependencyTargetFrameworks(XDocument nuspec)
        {
            return GetParsedDependencyGroups(nuspec)
                .Groups
                .ToLookup(x => x.ParsedTargetFramework, x => x.TargetFramework)
                .Where(x => x.Count() > 1)
                .SelectMany(x => x.Select(y => new
                {
                    ParsedTargetFramework = x.Key,
                    TargetFramework = y
                }))
                .ToLookup(x => x.ParsedTargetFramework, x => x.TargetFramework);
        }

        public static IReadOnlyList<string> GetCollidingMetadataElements(XDocument nuspec)
        {
            return GetMetadataElementNames(nuspec, onlyText: false)
                .Distinct()
                .Intersect(CollidingMetadataElementNames)
                .ToList();
        }

        public static ILookup<string, string> GetMetadataLookup(XDocument nuspec)
        {
            var metadataEl = GetMetadata(nuspec) ?? new XElement("metadata");

            return metadataEl
                .Elements()
                .ToLookup(x => x.Name.LocalName, x => x.Value);
        }

        public static IDictionary<string, int> GetDuplicateMetadataElements(XDocument nuspec, bool caseSensitive, bool onlyText)
        {
            var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

            return GetMetadataElementNames(nuspec, onlyText)
                .GroupBy(x => x, comparer)
                .Where(x => x.Count() > 1)
                .ToDictionary(x => x.Key, x => x.Count(), comparer);
        }

        public static IReadOnlyList<string> GetNonAlphabetMetadataElements(XDocument nuspec)
        {
            return GetMetadataElementNames(nuspec, onlyText: false)
                .Distinct()
                .Where(x => !IsAlphabet.IsMatch(x))
                .ToList();
        }

        private static IEnumerable<string> GetMetadataElementNames(XDocument nuspec, bool onlyText)
        {
            var metadataEl = GetMetadata(nuspec);
            if (metadataEl == null)
            {
                return Enumerable.Empty<string>();
            }

            var elements = metadataEl.Elements();

            if (onlyText)
            {
                elements = elements.Where(e => !e.HasElements && !string.IsNullOrEmpty(e.Value));
            }

            return elements.Select(x => x.Name.LocalName);
        }

        public static ILookup<string, string> GetUnexpectedValuesForBooleanMetadata(XDocument nuspec)
        {
            return GetMetadataLookup(nuspec)
                .Where(x => BooleanMetadataElementNames.Contains(x.Key))
                .SelectMany(x => x.Select(y => KeyValuePair.Create(x.Key, y?.Trim())))
                .Where(x => x.Value != "true" && x.Value != "false")
                .ToLookup(x => x.Key, x => x.Value);
        }

        public static ILookup<string, string> GetDuplicateDependencyTargetFrameworks(XDocument nuspec)
        {
            return GetParsedDependencyGroups(nuspec)
                .Groups
                .ToLookup(x => x.TargetFramework, x => x.TargetFramework)
                .Where(x => x.Count() > 1)
                .SelectMany(x => x)
                .ToLookup(x => x);
        }

        public static IEnumerable<string> GetInvalidDependencyTargetFrameworks(XDocument nuspec)
        {
            foreach (var group in GetParsedDependencyGroups(nuspec).Groups)
            {
                if (group.ParsedTargetFramework == null)
                {
                    yield return group.TargetFramework;
                }
            }
        }

        /// <summary>
        /// Interprets null or empty string as <see cref="NuGetFramework.AnyFramework"/>.
        /// Failed parsing is returned as null.
        /// </summary>
        private static NuGetFramework ParseTargetFramework(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return NuGetFramework.AnyFramework;
            }

            try
            {
                return NuGetFramework.Parse(input);
            }
            catch
            {
                return null;
            }
        }

        public static IEnumerable<string> GetWhitespaceDependencyTargetFrameworks(XDocument nuspec)
        {
            foreach (var group in GetParsedDependencyGroups(nuspec).Groups)
            {
                if (!string.IsNullOrEmpty(group.TargetFramework)
                    && string.IsNullOrWhiteSpace(group.TargetFramework))
                {
                    yield return group.TargetFramework;
                }
            }
        }

        public static IEnumerable<string> GetWhitespaceDependencyVersions(XDocument nuspec)
        {
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (!string.IsNullOrEmpty(dependency.Version)
                    && string.IsNullOrWhiteSpace(dependency.Version))
                {
                    yield return dependency.Version;
                }
            }
        }

        public static IEnumerable<string> GetInvalidDependencyVersions(XDocument nuspec)
        {
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (!string.IsNullOrWhiteSpace(dependency.Version)
                    && dependency.ParsedVersionRange == null)
                {
                    yield return dependency.Version;
                }
            }
        }

        public static IEnumerable<string> GetMissingDependencyIds(XDocument nuspec)
        {
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (dependency.Id == null)
                {
                    yield return dependency.Id;
                }
            }
        }

        public static IEnumerable<string> GetEmptyDependencyIds(XDocument nuspec)
        {
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (dependency.Id == string.Empty)
                {
                    yield return dependency.Id;
                }
            }
        }

        public static IEnumerable<string> GetWhitespaceDependencyIds(XDocument nuspec)
        {
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (!string.IsNullOrEmpty(dependency.Id)
                    && string.IsNullOrWhiteSpace(dependency.Id))
                {
                    yield return dependency.Id;
                }
            }
        }

        public static IEnumerable<string> GetMissingDependencyVersions(XDocument nuspec)
        {
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (dependency.Version == null)
                {
                    yield return dependency.Version;
                }
            }
        }

        public static IEnumerable<string> GetEmptyDependencyVersions(XDocument nuspec)
        {
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (dependency.Version == string.Empty)
                {
                    yield return dependency.Version;
                }
            }
        }

        public static DependencyGroups GetParsedDependencyGroups(XDocument nuspec)
        {
            var xmlDependencyGroups = GetXmlDependencyGroups(nuspec);

            var dependencies = GetParsedDependencies(xmlDependencyGroups.Dependencies);

            var groups = xmlDependencyGroups
                .Groups
                .Select(GetParsedDependencyGroup)
                .ToList();

            return new DependencyGroups(dependencies, groups);
        }

        private static DependencyGroup GetParsedDependencyGroup(XmlDependencyGroup group)
        {
            var targetFramework = group.TargetFramework;
            var parsedTargetFramework = ParseTargetFramework(targetFramework);

            var dependencies = GetParsedDependencies(group.Dependencies);

            return new DependencyGroup(
                targetFramework,
                parsedTargetFramework,
                dependencies);
        }

        private static IReadOnlyList<Dependency> GetParsedDependencies(IEnumerable<XmlDependency> dependencies)
        {
            return dependencies
                .Select(GetParsedDependency)
                .ToList();
        }

        private static XmlDependency GetXmlDependency(XElement dependencyEl)
        {
            var id = dependencyEl.Attribute("id")?.Value;
            var version = dependencyEl.Attribute("version")?.Value;
            return new XmlDependency(
                id,
                version,
                dependencyEl);
        }

        private static Dependency GetParsedDependency(XmlDependency dependency)
        {
            if (dependency.Version == null
                || !VersionRange.TryParse(dependency.Version, out var parsedVersionRange))
            {
                parsedVersionRange = null;
            }

            return new Dependency(
                dependency.Id,
                dependency.Version,
                parsedVersionRange);
        }

        public static ILookup<NuGetFramework, string> GetDuplicateDependencies(XDocument nuspec)
        {
            var groups = GetParsedDependencyGroups(nuspec);

            return groups
                .Groups
                .Concat(new[]
                {
                    new DependencyGroup(
                        null,
                        null,
                        groups.Dependencies)
                })
                .Select(x => new DependencyGroup(
                    x.TargetFramework,
                    string.IsNullOrEmpty(x.TargetFramework) ? NuGetFramework.AnyFramework : x.ParsedTargetFramework,
                    x.Dependencies))
                .SelectMany(x => x
                    .Dependencies
                    .Select(y => new { x.ParsedTargetFramework, y.Id }))
                .ToLookup(
                    x => x.ParsedTargetFramework,
                    x => x.Id)
                .Select(x => new
                {
                    ParsedTargetFramework = x.Key,
                    DuplicateIds = x
                        .ToLookup(y => y, StringComparer.OrdinalIgnoreCase)
                        .Where(y => y.Count() > 1)
                        .Select(y => y.Key),
                })
                .SelectMany(x => x
                    .DuplicateIds
                    .Select(y => new
                    {
                        ParsedTargetFramework = x.ParsedTargetFramework,
                        Id = y,
                    }))
                .ToLookup(
                    x => x.ParsedTargetFramework,
                    x => x.Id);
        }

        public static IEnumerable<string> GetFloatingDependencyVersions(XDocument nuspec)
        {
            foreach (var dependency in GetDependencies(nuspec))
            {
                if (dependency.ParsedVersionRange != null
                    && dependency.ParsedVersionRange.IsFloating)
                {
                    yield return dependency.Version;
                }
            }
        }
    }
}
