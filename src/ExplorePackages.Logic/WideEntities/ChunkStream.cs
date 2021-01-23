using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class ChunkStream : Stream
    {
        private readonly IReadOnlyList<ReadOnlyMemory<byte>> _chunks;
        private int _chunkIndex;
        private int _offsetBeforeChunk;
        private int _offsetInsideChunk;

        public ChunkStream(IReadOnlyList<ReadOnlyMemory<byte>> chunks)
        {
            _chunks = chunks.Where(x => x.Length > 0).ToList();
            if (_chunks.Count > 0)
            {
                Length = _chunks.Sum(x => x.Length);
            }
            else
            {
                Length = 0;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }

        public override long Position
        {
            get => _offsetBeforeChunk + _offsetInsideChunk;

            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The stream position must not be negative.");
                }

                int chunkIndex;
                var offsetBeforeChunk = 0;
                var offsetInsideChunk = 0;
                for (chunkIndex = 0; chunkIndex < _chunks.Count && offsetBeforeChunk + offsetInsideChunk < value; chunkIndex++)
                {
                    var chunk = _chunks[chunkIndex];
                    var remaining = value - offsetBeforeChunk;
                    if (remaining >= chunk.Length)
                    {
                        offsetBeforeChunk += chunk.Length;
                    }
                    else
                    {
                        offsetInsideChunk = (int)remaining;
                        chunkIndex -= 1;
                    }
                }

                _chunkIndex = chunkIndex;
                _offsetBeforeChunk = offsetBeforeChunk;
                _offsetInsideChunk = offsetInsideChunk;
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Length == 0 || count == 0 || _offsetBeforeChunk >= Length)
            {
                return 0;
            }

            var currentOffset = offset;
            var endOffset = offset + count;


            do
            {
                // How much should we read from the current chunk?
                var currentChunk = _chunks[_chunkIndex];
                var copyCount = Math.Min(currentChunk.Length - _offsetInsideChunk, endOffset - currentOffset);

                currentChunk.Slice(_offsetInsideChunk, copyCount).CopyTo(buffer.AsMemory(currentOffset));
                currentOffset += copyCount;
                _offsetInsideChunk += copyCount;

                if (_offsetInsideChunk >= currentChunk.Length)
                {
                    _chunkIndex++;
                    _offsetBeforeChunk += currentChunk.Length;
                    _offsetInsideChunk = 0;
                }
            }
            while (currentOffset < endOffset && _offsetBeforeChunk < Length);

            return currentOffset - offset;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
                default:
                    throw new ArgumentException("Unrecognzied seek origin.", nameof(origin));
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
