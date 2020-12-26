using System.IO;

namespace Knapcode.ExplorePackages
{
    public class TempStreamResult
    {
        private TempStreamResult(bool success, Stream stream, byte[] hash)
        {
            Success = success;
            Stream = stream;
            Hash = hash;
        }

        public bool Success { get; }
        public Stream Stream { get; }
        public byte[] Hash { get; }

        public static TempStreamResult NewSuccess(Stream stream, byte[] hash)
        {
            return new TempStreamResult(success: true, stream, hash);
        }

        public static TempStreamResult NewFailure()
        {
            return new TempStreamResult(success: false, stream: null, hash: null);
        }
    }
}
