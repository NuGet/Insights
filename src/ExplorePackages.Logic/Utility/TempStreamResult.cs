using System.IO;

namespace Knapcode.ExplorePackages
{
    public class TempStreamResult
    {
        private TempStreamResult(bool success, Stream stream)
        {
            Success = success;
            Stream = stream;
        }

        public bool Success { get; }
        public Stream Stream { get; }

        public static TempStreamResult NewSuccess(Stream stream)
        {
            return new TempStreamResult(success: true, stream);
        }

        public static TempStreamResult NewFailure()
        {
            return new TempStreamResult(success: false, stream: null);
        }
    }
}
