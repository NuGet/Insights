namespace Knapcode.ExplorePackages
{
    public class TempStreamDirectory
    {
        public string Path { get; set; }
        public int? MaxConcurrentWriters { get; set; }
        public bool PreallocateFile { get; set; } = true;

        public static implicit operator TempStreamDirectory(string Path) => new TempStreamDirectory { Path = Path };
        public static implicit operator string(TempStreamDirectory dir) => dir.Path;

        public override string ToString() => Path;
    }
}
