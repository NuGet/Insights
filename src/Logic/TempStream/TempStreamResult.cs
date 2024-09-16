// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class TempStreamResult : IAsyncDisposable
    {
        private readonly Stream? _stream;
        private readonly HashOutput? _hash;
        private readonly IAsyncDisposable? _lease;

        private TempStreamResult(TempStreamResultType type, Stream? stream, HashOutput? hash, IAsyncDisposable? lease)
        {
            Type = type;
            _stream = stream;
            _hash = hash;
            _lease = lease;
        }

        public TempStreamResultType Type { get; }
        public Stream Stream => Type == TempStreamResultType.Success ? _stream! : throw new InvalidOperationException($"No stream available. Result type is {Type}.");
        public HashOutput Hash => Type == TempStreamResultType.Success ? _hash! : throw new InvalidOperationException($"No hash available. Result type is {Type}.");

        public static TempStreamResult Success(Stream stream, HashOutput hash, IAsyncDisposable lease)
        {
            return new TempStreamResult(TempStreamResultType.Success, stream, hash, lease);
        }

        public static TempStreamResult NeedNewStream()
        {
            return new TempStreamResult(TempStreamResultType.NeedNewStream, stream: null, hash: null, lease: null);
        }

        public static TempStreamResult SemaphoreNotAvailable()
        {
            return new TempStreamResult(TempStreamResultType.SemaphoreNotAvailable, stream: null, hash: null, lease: null);
        }

        public async ValueTask DisposeAsync()
        {
            _stream?.Dispose();
            if (_lease is not null)
            {
                try
                {
                    await _lease.DisposeAsync();
                }
                catch
                {
                    // Best effort.
                }
            }
        }
    }
}
