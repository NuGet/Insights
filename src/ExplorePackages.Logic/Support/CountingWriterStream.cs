using System;
using System.IO;

namespace Knapcode.ExplorePackages
{
    public class CountingWriterStream : Stream
    {
        private readonly Stream _inner;
        private long _length;

        public CountingWriterStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _length += count;
            _inner.Write(buffer, offset, count);
        }
    }
}
