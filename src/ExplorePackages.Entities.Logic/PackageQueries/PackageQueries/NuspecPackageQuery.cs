using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecPackageQuery : IPackageQuery
    {
        private readonly INuspecQuery _nuspecQuery;

        public NuspecPackageQuery(INuspecQuery nuspecQuery)
        {
            _nuspecQuery = nuspecQuery;
        }

        public string Name => _nuspecQuery.Name;
        public string CursorName => _nuspecQuery.CursorName;
        public bool NeedsNuspec => true;
        public bool NeedsMZip => false;
        public bool IsV2Query => false;
        public TimeSpan Delay => TimeSpan.Zero;

        public Task<bool> IsMatchAsync(PackageQueryContext context, PackageConsistencyState state)
        {
            return Task.FromResult(IsMatch(context));
        }

        private bool IsMatch(PackageQueryContext context)
        {
            if (context.Nuspec.Document == null)
            {
                return false;
            }

            return _nuspecQuery.IsMatch(context.Nuspec.Document);
        }
    }
}
