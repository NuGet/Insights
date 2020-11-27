using System.Security.Cryptography;

namespace Knapcode.ExplorePackages
{
    public class MD5IncrementalHash : IIncrementalHash
    {
        private readonly MD5 _implementation;

        public MD5IncrementalHash()
        {
            _implementation = MD5.Create();
        }

        public void AppendData(byte[] data, int offset, int count)
        {
            _implementation.TransformBlock(data, offset, count, null, 0);
        }

        public byte[] GetHashAndReset()
        {
            _implementation.TransformFinalBlock(new byte[0], 0, 0);
            var hash = _implementation.Hash;
            _implementation.Initialize();

            return hash;
        }

        public void Dispose()
        {
            _implementation.Dispose();
        }
    }
}
