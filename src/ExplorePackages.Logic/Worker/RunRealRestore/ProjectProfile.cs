using NuGet.Frameworks;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class ProjectProfile
    {
        public ProjectProfile(NuGetFramework framework, string templateName, string templatePackageId, NuGetVersion templatePackageVersion)
        {
            Framework = framework;
            TemplateName = templateName;
            TemplatePackageId = templatePackageId;
            TemplatePackageVersion = templatePackageVersion;
        }

        public NuGetFramework Framework { get; }
        public string TemplateName { get; }
        public string TemplatePackageId { get; }
        public NuGetVersion TemplatePackageVersion { get; }
    }
}
