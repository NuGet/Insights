using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class ChunkStreamTest
    {
        private readonly Random _random = new Random(0);

        [Theory]
        [InlineData("100,50,200", 0, 500, 0, 50)] // Beginning of first chunk
        [InlineData("100,50,200", 0, 500, 25, 50)] // Beginning of first chunk, offset in buffer
        [InlineData("100,50,200", 0, 500, 0, 100)] // Entire first chunk
        [InlineData("100,50,200", 100, 500, 0, 50)] // Entire second chunk
        [InlineData("100,50,200", 150, 500, 0, 200)] // Entire third chunk
        [InlineData("100,50,200", 75, 500, 0, 50)] // Spanning two chunks
        [InlineData("100,50,200", 75, 500, 0, 100)] // Spanning three chunks
        [InlineData("100,50,200", 0, 500, 0, 350)] // All chunks, matching count
        [InlineData("100,50,200", 0, 500, 25, 350)] // All chunks, matching count, offset in buffer
        [InlineData("100,50,200", 0, 500, 0, 450)] // All chunks, greater count
        [InlineData("100,50,200", 0, 500, 25, 450)] // All chunks, greater count, offset in buffer
        [InlineData("100,50,200", 50, 500, 0, 350)] // Not at the start, rest of stream
        [InlineData("100,50,200", 50, 500, 25, 350)] // Not at the start, rest of stream, offset in buffer
        [InlineData("100", 0, 500, 0, 50)] // Part of a single chunk
        [InlineData("100", 0, 500, 25, 50)] // Part of a single chunk, offset in buffer
        [InlineData("100", 0, 500, 0, 100)] // All of a single chunk
        [InlineData("100", 0, 500, 25, 100)] // All of a single chunk, offset in buffer
        [InlineData("100", 0, 500, 0, 150)] // More than the single chunk
        [InlineData("100", 0, 500, 25, 150)] // More than the single chunk, offset in buffer
        [InlineData("0,100,0,0,1,0,49,0", 0, 500, 0, 50)] // Empty chunks, partial read
        [InlineData("0,100,0,0,1,0,49,0", 0, 500, 0, 150)] // Empty chunks, full read
        [InlineData("0,100,0,0,1,0,49,0", 0, 500, 25, 150)] // Empty chunks, full read, offset in buffer
        [InlineData("0,100,0,0,1,0,49,0", 25, 500, 25, 150)] // Empty chunks, part of first chunk, offset in buffer
        [InlineData("0,100,0,0,1,0,49,0", 0, 500, 0, 200)] // Empty chunks, more than full read
        [InlineData("0,1,2,3,4,5,6,7,8,9,10", 0, 500, 0, 200)] // Ascending chunk sizes
        [InlineData("0,1,2,3,4,5,6,7,8,9,10", 20, 500, 20, 200)] // scending chunk sizes with initial position
        public void ReadsCorrectBytesForSingleRead(string chunkLengths, int initialPosition, int bufferSize, int offset, int count)
        {
            // Arrange
            var chunkLengthInts = chunkLengths.Split(',').Select(int.Parse).ToArray();
            (var chunks, var bytes) = GetChunks(chunkLengthInts);

            var target = new ChunkStream(chunks)
            {
                Position = initialPosition
            };

            var buffer = new byte[bufferSize];

            // Act
            var actualRead = target.Read(buffer, offset, count);

            // Assert
            VerifyResult(target, bytes, initialPosition, buffer, offset, count, actualRead);
        }

        [Fact]
        public void AllowsEmpty()
        {
            // Arrange
            var target = new ChunkStream(new List<ReadOnlyMemory<byte>>());
            var buffer = new byte[500];
            var offset = 0;
            var count = 100;

            // Act
            var actualRead = target.Read(buffer, offset, count);

            // Assert
            VerifyResult(target, Array.Empty<byte>(), 0, buffer, offset, count, actualRead);
        }

        [Fact]
        public async Task AllowsAsyncRead()
        {
            // Arrange
            (var chunks, var bytes) = GetChunks(100, 50, 200);

            var target = new ChunkStream(chunks);
            var initialPosition = 5;
            target.Position = initialPosition;

            var buffer = new byte[500];
            var offset = 25;
            var count = 400;

            // Act
            var actualRead = await target.ReadAsync(buffer, offset, count);

            // Assert
            VerifyResult(target, bytes, initialPosition, buffer, offset, count, actualRead);
        }

        [Fact]
        public void AllowsMultipleReads()
        {
            // Arrange
            (var chunks, var bytes) = GetChunks(100, 50, 200);
            var target = new ChunkStream(chunks);
            var buffer = new byte[1000];

            // Act
            Assert.Equal(75, target.Read(buffer, 0, count: 75));
            Assert.Equal(100, target.Read(buffer, 75, count: 100));
            Assert.Equal(175, target.Read(buffer, 175, count: 500));

            // Assert
            VerifyResult(target, bytes, 0, buffer, 0, 350, 350);
        }

        [Fact]
        public void AllowRereadingMoreData()
        {
            // Arrange
            (var chunks, var bytes) = GetChunks(100, 50, 200);
            var target = new ChunkStream(chunks);
            var buffer = new byte[1000];

            // Act
            Assert.Equal(100, target.Read(buffer, 0, count: 100));
            target.Position = 0;
            Assert.Equal(200, target.Read(buffer, 0, count: 200));

            // Assert
            VerifyResult(target, bytes, 0, buffer, 0, 200, 200);
        }

        [Fact]
        public void AllowRereadingLessData()
        {
            // Arrange
            (var chunks, var bytes) = GetChunks(100, 50, 200);
            var target = new ChunkStream(chunks);
            var buffer = new byte[1000];

            // Act
            Assert.Equal(200, target.Read(buffer, 0, count: 200));
            target.Position = 0;
            Assert.Equal(100, target.Read(buffer, 0, count: 100));
            target.Position = 200;

            // Assert
            VerifyResult(target, bytes, 0, buffer, 0, 200, 200);
        }

        private static void VerifyResult(ChunkStream target, byte[] bytes, int initialPosition, byte[] buffer, int offset, int count, int actualRead)
        {
            var empty = new byte[buffer.Length];
            var expectedRead = Math.Min(count, bytes.Length - initialPosition);
            Assert.Equal(expectedRead, actualRead);
            Assert.Equal(bytes.AsSpan(initialPosition, expectedRead).ToArray(), buffer.AsSpan(offset, expectedRead).ToArray());

            // Verify before the offset is still zeros.
            if (offset > 0)
            {
                Assert.Equal(empty.AsSpan(0, offset).ToArray(), buffer.AsSpan(0, offset).ToArray());
            }

            // Verify after the read is still zeros
            var afterOffset = offset + expectedRead;
            var afterCount = buffer.Length - afterOffset;
            if (afterCount > 0)
            {
                Assert.Equal(empty.AsSpan(afterOffset, afterCount).ToArray(), buffer.AsSpan(afterOffset, afterCount).ToArray());
            }

            // Verify final position
            Assert.Equal(initialPosition + expectedRead, target.Position);

            // Verify length
            Assert.Equal(bytes.Length, target.Length);
        }

        private (IReadOnlyList<ReadOnlyMemory<byte>> Chunks, byte[] Bytes) GetChunks(params int[] chunkLengths)
        {
            var chunks = chunkLengths
                .Select(GetChunk)
                .Select(x => (ReadOnlyMemory<byte>)x.AsMemory())
                .ToList();

            using var memoryStream = new MemoryStream();
            foreach (var chunk in chunks)
            {
                memoryStream.Write(chunk.Span);
            }
            var bytes = memoryStream.ToArray();

            return (chunks, bytes);
        }

        private byte[] GetChunk(int length)
        {
            var chunk = new byte[length];
            _random.NextBytes(chunk);
            return chunk;
        }
    }
}
