# ExplorePackages

Explore packages on a V3 NuGet package source that has a catalog (NuGet.org!).

## Purpose

The purpose of this repository is to explore oddities and inconsistencies on NuGet.org's available packages. To support
this goal, I've built a generic way of writing "queries" which search for packages with arbitrary characteristics. For
example, to support my repository metadata proposal ([NuGet/NuGetGallery#4941](https://github.com/NuGet/NuGetGallery/issues/4941))
I wrote a query to find all packages on NuGet.org that have the `<repository>` element in there .nuspec:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class FindRepositoriesNuspecQuery : INuspecQuery
    {
        public string Name => PackageQueryNames.FindRepositoriesNuspecQuery;
        public string CursorName => CursorNames.FindRepositoriesNuspecQuery;
        
        public bool IsMatch(XDocument nuspec)
        {
            var repositoryEl = NuspecUtility.GetRepository(nuspec);

            return repositoryEl != null;
        }
    }

    public static class NuspecUtility
    {
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
    }
}
```

### Queries

I've written the following queries, but you can fork this repository and search for anything you want.
https://github.com/joelverhagen/ExplorePackages/tree/master/ExplorePackages/Logic/PackageQueries

### Consistency

I've also used this framework to look for inconsistencies in the V2 and V3 endpoints. To make this easier, I made a
little website to see if your package is fully propagated on NuGet.org (that is, the indexing is complete).

https://explorepackages.azurewebsites.net/
