// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Insights
{
    public class LimitStream : Stream
    {
        private readonly Stream _stream;

        public LimitStream(Stream stream, int limitBytes)
        {
            if (limitBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limitBytes), limitBytes, "The limit in bytes must be greater than or equal to zero.");
            }

            _stream = stream;
            LimitBytes = limitBytes;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => Math.Min(LimitBytes, _stream.Length);
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public int LimitBytes { get; }
        public bool Truncated { get; private set; }
        public int ReadBytes { get; private set; }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Truncated)
            {
                return 0;
            }

            var remainingCount = LimitBytes - ReadBytes;
            var limitCount = Math.Min(count, remainingCount + 1);
            var couldTruncate = limitCount == remainingCount + 1;
            var extraByte = couldTruncate ? buffer[offset + (limitCount - 1)] : default;
            var read = _stream.Read(buffer, offset, limitCount);

            if (read > remainingCount)
            {
                read--;
                buffer[offset + (limitCount - 1)] = extraByte;
                Truncated = true;
            }

            ReadBytes += read;

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _stream.Dispose();
        }
    }
}
