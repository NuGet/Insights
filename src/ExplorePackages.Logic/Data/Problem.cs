namespace Knapcode.ExplorePackages
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
