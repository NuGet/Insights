namespace Knapcode.ExplorePackages
{
    public class HashOutput
    {
        public byte[] MD5 { get; internal set; }
        public byte[] SHA1 { get; internal set; }
        public byte[] SHA256 { get; internal set; }
        public byte[] SHA512 { get; internal set; }
    }
}
