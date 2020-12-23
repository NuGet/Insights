namespace Knapcode.ExplorePackages
{
    public class TempStreamDirectory
    {
        public int? MaxConcurrentWriters { get; set; }
        public string Path { get; set; }

        public static implicit operator TempStreamDirectory(string Path) => new TempStreamDirectory { Path = Path };
        public static implicit operator string(TempStreamDirectory dir) => dir.Path;
    }
}
