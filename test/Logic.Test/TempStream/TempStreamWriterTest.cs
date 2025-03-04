// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class TempStreamWriterTest
    {
        public class TheGetTempFileNameFactoryMethod
        {
            [Theory]
            [InlineData("processor", ".dll", "_processor.dll")]
            [InlineData("processor", ".foo.dll", "_processor.dll")]
            [InlineData("processor", ".foo-dll", "_processor.tmp")]
            [InlineData("processor", "dll", "_processor.dll")]
            [InlineData("processor", "", "_processor.tmp")]
            [InlineData("", ".dll", ".dll")]
            [InlineData(null, null, ".tmp")]
            public void ReturnsExpectedFileNameSuffix(string? contextHint, string? extension, string expected)
            {
                // Arrange
                var factory = TempStreamWriter.GetTempFileNameFactory("NuGet.Versioning", "3.5.0", contextHint, extension);

                // Act
                var tempFileName = factory();

                // Assert
                Assert.EndsWith("_NuGet.Versioning_3.5.0" + expected, tempFileName, StringComparison.Ordinal);
            }
        }

        public class TheCopyToTempStreamAsyncMethod : BaseLogicIntegrationTest
        {
            [Theory]
            [MemberData(nameof(StreamSizes))]
            public async Task CanCopyStreamWithUnknownLength(int length)
            {
                // Arrange
                var buffer = new byte[length];
                new Random(Seed: length).NextBytes(buffer);
                var data = new StreamWithUnknownLength(new MemoryStream(buffer));
                var writer = GetTarget();

                // Act
                var result = await writer.CopyToTempStreamAsync(data, () => "test", -1, IncrementalHash.CreateNone());

                // Assert
                using var tempStream = result.Stream;
                Assert.Equal(TempStreamResultType.Success, result.Type);
                var output = new MemoryStream();
                await tempStream.CopyToAsync(output);
                Assert.Equal(buffer, output.ToArray());
                Assert.IsType<FileStream>(tempStream);
            }

            [Theory]
            [MemberData(nameof(StreamSizes))]
            public async Task CanCopyStreamWithTooLongExplicitLength(int length)
            {
                // Arrange
                var buffer = new byte[length];
                new Random(Seed: length).NextBytes(buffer);
                var data = new MemoryStream(buffer);
                var writer = GetTarget();

                // Act
                var result = await writer.CopyToTempStreamAsync(data, () => "test", StreamSizes.Max(x => (int)x[0]) + 1, IncrementalHash.CreateNone());

                // Assert
                using var tempStream = result.Stream;
                Assert.Equal(TempStreamResultType.Success, result.Type);
                var output = new MemoryStream();
                await tempStream.CopyToAsync(output);
                Assert.Equal(buffer, output.ToArray());
                Assert.IsType<FileStream>(tempStream);
            }

            [Theory]
            [MemberData(nameof(StreamSizes))]
            public async Task CanCopyStreamWithExplicitLength(int length)
            {
                // Arrange
                var buffer = new byte[length];
                new Random(Seed: length).NextBytes(buffer);
                var data = new MemoryStream(buffer);
                var writer = GetTarget();

                // Act
                var result = await writer.CopyToTempStreamAsync(data, () => "test", length, IncrementalHash.CreateNone());

                // Assert
                using var tempStream = result.Stream;
                Assert.Equal(TempStreamResultType.Success, result.Type);
                var output = new MemoryStream();
                await tempStream.CopyToAsync(output);
                Assert.Equal(buffer, output.ToArray());
                Assert.IsType<FileStream>(tempStream);
            }

            [Theory]
            [MemberData(nameof(StreamSizes))]
            public async Task CanCopyStreamWithTooShortExplicitLength(int length)
            {
                // Arrange
                var buffer = new byte[length];
                new Random(Seed: length).NextBytes(buffer);
                var data = new MemoryStream(buffer);
                var writer = GetTarget();

                // Act
                var result = await writer.CopyToTempStreamAsync(data, () => "test", 1, IncrementalHash.CreateNone());

                // Assert
                using var tempStream = result.Stream;
                Assert.Equal(TempStreamResultType.Success, result.Type);
                var output = new MemoryStream();
                await tempStream.CopyToAsync(output);
                Assert.Equal(buffer, output.ToArray());
                Assert.IsType<FileStream>(tempStream);
            }

            [Theory]
            [MemberData(nameof(StreamSizes))]
            public async Task CanCopyStreamWithStreamLength(int length)
            {
                // Arrange
                var buffer = new byte[length];
                new Random(Seed: length).NextBytes(buffer);
                var data = new MemoryStream(buffer);
                var writer = GetTarget();

                // Act
                var result = await writer.CopyToTempStreamAsync(data, () => "test", -1, IncrementalHash.CreateNone());

                // Assert
                using var tempStream = result.Stream;
                Assert.Equal(TempStreamResultType.Success, result.Type);
                var output = new MemoryStream();
                await tempStream.CopyToAsync(output);
                Assert.Equal(buffer, output.ToArray());
                Assert.IsType<FileStream>(tempStream);
            }

            public static IEnumerable<object[]> StreamSizes = new int[]
            {
                0,
                100,
                64 * 1024,
                128 * 1024 - 1,
                128 * 1024,
                128 * 1024 + 1,
                256 * 1024 - 1,
                256 * 1024,
                256 * 1024 + 1,
                1024 * 1024 - 1,
                1024 * 1024,
                1024 * 1024 + 1,
                4 * 1024 * 1024 - 1,
                4 * 1024 * 1024,
                4 * 1024 * 1024 + 1,
            }.Select(x => new object[] { x });

            public TempStreamWriter GetTarget() => Host.Services.GetRequiredService<Func<TempStreamWriter>>()();

            public TheCopyToTempStreamAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
                ConfigureSettings = x => x.MaxTempMemoryStreamSize = -1;
            }
        }

        private class StreamWithUnknownLength : Stream
        {
            private readonly Stream _inner;

            public StreamWithUnknownLength(Stream inner)
            {
                _inner = inner;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, count);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _inner.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
