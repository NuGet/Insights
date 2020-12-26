using System;
using System.IO;

namespace Knapcode.ExplorePackages
{
    public class TempStreamResult : IDisposable
    {
        private readonly Stream _stream;
        private readonly byte[] _hash;

        private TempStreamResult(TempStreamResultType type, Stream stream, byte[] hash)
        {
            Type = type;
            _stream = stream;
            _hash = hash;
        }

        public TempStreamResultType Type { get; }
        public Stream Stream => Type == TempStreamResultType.Success ? _stream : throw new InvalidOperationException($"No stream available. Result type is {Type}.");
        public byte[] Hash => Type == TempStreamResultType.Success ? _hash : throw new InvalidOperationException($"No hash available. Result type is {Type}.");

        public static TempStreamResult Success(Stream stream, byte[] hash)
        {
            return new TempStreamResult(TempStreamResultType.Success, stream, hash);
        }

        public static TempStreamResult NeedNewStream()
        {
            return new TempStreamResult(TempStreamResultType.NeedNewStream, stream: null, hash: null);
        }

        public static TempStreamResult SemaphoreTimeout()
        {
            return new TempStreamResult(TempStreamResultType.SemaphoreTimeout, stream: null, hash: null);
        }

        public void Dispose() => _stream?.Dispose();
    }
}
