using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public static class NuspecUtility
    {
        private static readonly NuGetFrameworkNameComparer NuGetFrameworkNameComparer = new NuGetFrameworkNameComparer();

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

        public static XmlDependencyGroups GetDependencyGroups(XDocument nuspec)
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
                .ToList();

            var groups = new List<XmlDependencyGroup>();
            foreach (var groupEl in dependenciesEl.Elements(groupName))
            {
                var targetFramework = groupEl.Attribute("targetFramework")?.Value;
                var groupDependencies = groupEl
                    .Elements(dependencyName)
                    .ToList();
                groups.Add(new XmlDependencyGroup(
                    targetFramework,
                    groupDependencies));
            }

            return new XmlDependencyGroups(legacyDependencies, groups);
        }

        public static IReadOnlyList<XElement> GetDependencies(XDocument nuspec)
        {
            var groups = GetDependencyGroups(nuspec);
            return groups
                .Groups
                .SelectMany(x => x.Dependencies)
                .Concat(groups.Dependencies)
                .ToList();
        }

        public static IEnumerable<string> GetInvalidDependencyIds(XDocument nuspec)
        {
            var dependencyEls = GetDependencies(nuspec);

            foreach (var dependencyEl in dependencyEls)
            {
                var id = dependencyEl.Attribute("id")?.Value;
                if (!StrictPackageIdValidator.IsValid(id))
                {
                    yield return id;
                }
            }
        }

        public static bool HasMixedDependencyGroupStyles(XDocument nuspec)
        {
            var groups = GetDependencyGroups(nuspec);

            return groups.Dependencies.Any() && groups.Groups.Any();
        }

        public static IEnumerable<string> GetDependencyTargetFrameworks(XDocument nuspec)
        {
            var groups = GetDependencyGroups(nuspec);

            foreach (var group in groups.Groups)
            {
                if (string.IsNullOrEmpty(group.TargetFramework))
                {
                    continue;
                }

                yield return group.TargetFramework;
            }
        }

        public static IEnumerable<string> GetUnsupportedDependencyTargetFrameworks(XDocument nuspec)
        {
            foreach (var targetFramework in GetDependencyTargetFrameworks(nuspec))
            {
                var unsupported = false;
                try
                {
                    var parsedFramework = NuGetFramework.Parse(targetFramework);
                    if (NuGetFrameworkNameComparer.Equals(parsedFramework, NuGetFramework.UnsupportedFramework))
                    {
                        unsupported = true;
                    }
                }
                catch
                {
                    continue;
                }

                if (unsupported)
                {
                    yield return targetFramework;
                }
            }
        }

        public static IReadOnlyDictionary<NuGetFramework, IReadOnlyList<string>> GetDuplicateNormalizedDependencyTargetFrameworks(XDocument nuspec)
        {
            return GetDependencyTargetFrameworks(nuspec)
                .Where(x => IsValidTargetFramework(x))
                .GroupBy(x => NuGetFramework.Parse(x ?? string.Empty))
                .Where(x => x.Count() > 1)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<string>) x.ToList());
        }

        public static IReadOnlyDictionary<string, int> GetDuplicateDependencyTargetFrameworks(XDocument nuspec)
        {
            return GetDependencyTargetFrameworks(nuspec)
                .Select(x => x ?? string.Empty)
                .GroupBy(x => x)
                .Where(x => x.Count() > 1)
                .ToDictionary(x => x.Key, x => x.Count());
        }

        public static IEnumerable<string> GetInvalidDependencyTargetFrameworks(XDocument nuspec)
        {
            foreach (var targetFramework in GetDependencyTargetFrameworks(nuspec))
            {
                if (string.IsNullOrEmpty(targetFramework))
                {
                    continue;
                }

                if (!IsValidTargetFramework(targetFramework))
                {
                    yield return targetFramework;
                }
            }
        }

        private static bool IsValidTargetFramework(string input)
        {
            try
            {
                NuGetFramework.Parse(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static IEnumerable<string> GetWhitespaceDependencyTargetFrameworks(XDocument nuspec)
        {
            foreach (var group in GetDependencyGroups(nuspec).Groups)
            {
                if (!string.IsNullOrEmpty(group.TargetFramework)
                    && string.IsNullOrWhiteSpace(group.TargetFramework))
                {
                    yield return group.TargetFramework;
                }
            }
        }

        public static IEnumerable<string> GetInvalidDependencyVersions(XDocument nuspec)
        {
            foreach (var dependencyEl in GetDependencies(nuspec))
            {
                var version = dependencyEl.Attribute("version")?.Value;
                if (!string.IsNullOrEmpty(version)
                    && !VersionRange.TryParse(version, out var parsed))
                {
                    yield return version;
                }
            }
        }

        public static IEnumerable<string> GetMissingDependencyIds(XDocument nuspec)
        {
            foreach (var dependencyEl in GetDependencies(nuspec))
            {
                var id = dependencyEl.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id))
                {
                    yield return id;
                }
            }
        }

        public static IEnumerable<string> GetMissingDependencyVersions(XDocument nuspec)
        {
            foreach (var dependencyEl in GetDependencies(nuspec))
            {
                var version = dependencyEl.Attribute("version");
                if (version == null)
                {
                    yield return null;
                }
            }
        }

        public static IEnumerable<string> GetEmptyDependencyVersions(XDocument nuspec)
        {
            foreach (var dependencyEl in GetDependencies(nuspec))
            {
                var version = dependencyEl.Attribute("version");
                if (version != null && version.Value == string.Empty)
                {
                    yield return string.Empty;
                }
            }
        }
    }
}
