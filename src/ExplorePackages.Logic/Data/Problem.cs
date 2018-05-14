namespace Knapcode.ExplorePackages.Logic
{
    public class Problem
    {
        public Problem(PackageIdentity package, string problemId)
        {
            PackageIdentity = package;
            ProblemId = problemId;
        }

        public PackageIdentity PackageIdentity { get; }
        public string ProblemId { get; }
    }
}
