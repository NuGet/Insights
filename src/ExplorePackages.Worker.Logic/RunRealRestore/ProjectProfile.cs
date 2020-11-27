using NuGet.Frameworks;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Worker.RunRealRestore
{
    public class ProjectProfile
    {
        public ProjectProfile(NuGetFramework framework, string templateName, NuGetPackageIdentity templatePackage)
        {
            Framework = framework;
            TemplateName = templateName;
            TemplatePackage = templatePackage;
        }

        public NuGetFramework Framework { get; }
        public string TemplateName { get; }
        public NuGetPackageIdentity TemplatePackage { get; }
    }
}
